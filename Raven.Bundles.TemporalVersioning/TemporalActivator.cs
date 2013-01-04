using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Logging;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database;
using Raven.Database.Plugins;

namespace Raven.Bundles.TemporalVersioning
{
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalActivator : IStartupTask, IDisposable
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
        private readonly Timer _timer;
        private volatile bool _executing;
        private DocumentDatabase _database;
        private DateTime _nextRunDate = DateTime.MaxValue;

        public TemporalActivator()
        {
            _timer = new Timer(TimerElapsed);
        }

        public void Execute(DocumentDatabase database)
        {
            _database = database;

            TemporalRevisionsIndex.CreateIndex(database);

            ResetTimer(TemporalRevisionsIndex.GetNextPendingDate(_database), SystemTime.UtcNow);
        }

        public void Dispose()
        {
            if (_timer != null)
                _timer.Dispose();
        }

        /// <summary>
        /// Resets the activation timer to fire when the next future revision of any document needs to become current.
        /// </summary>
        /// <remarks>
        /// This is called any time a new revision is stored, preventing us from having to poll periodically.
        /// </remarks>
        internal void ResetTimer(DateTime runDate, DateTime now)
        {
            // Don't wait at all if we were asked to wait forever.
            if (runDate == DateTime.MaxValue)
                return;

            // Never wait more than an hour from now.  Running early is ok.
            if (runDate > now.AddHours(1))
                runDate = now.AddHours(1);

            // If rundate as passed, use now.
            if (runDate < now)
                runDate = now;

            // If the rundate is later than we're already waiting, then ignore it.
            if (runDate >= _nextRunDate)
                return;

            // Hold on to the date for comparison next time around
            _nextRunDate = runDate;

            // Determine the wait time
            var wait = (long) (runDate - now).TotalMilliseconds;

            // Set the timer.
            _timer.Change(wait, -1);
        }

        private void TimerElapsed(object state)
        {
            if (_executing)
                return;

            _executing = true;
            _nextRunDate = DateTime.MaxValue;

            try
            {
                ActivatePendingDocuments();
            }
            catch (Exception e)
            {
                _log.ErrorException("Error when trying to activate temporal revision documents", e);
            }
            finally
            {
                ResetTimer(TemporalRevisionsIndex.GetNextPendingDate(_database), SystemTime.UtcNow);
                _executing = false;
            }
        }

        private void ActivatePendingDocuments()
        {
            using (_database.DisableAllTriggersForCurrentThread())
            {
                var revisionKeys = TemporalRevisionsIndex.GetPendingDocumentsRequiringActivation(_database);

                foreach (var revisionkey in revisionKeys)
                {
                    _log.Info("Activating Temporal Document {0}", revisionkey);

                    // Establish a new transaction
                    var transactionInformation = new TransactionInformation { Id = Guid.NewGuid(), Timeout = TimeSpan.FromMinutes(1) };

                    // Mark the document as non-pending
                    _database.SetDocumentMetadata(revisionkey, transactionInformation, TemporalMetadata.RavenDocumentTemporalPending, false);

                    // Get the current key from the revision key
                    var currentKey = revisionkey.Substring(0, revisionkey.IndexOf(TemporalConstants.TemporalKeySeparator, StringComparison.Ordinal));

                    // Load the new revisions document
                    var newRevisionDoc = _database.Get(revisionkey, transactionInformation);
                    var temporal = newRevisionDoc.Metadata.GetTemporalMetadata();
                    if (temporal.Deleted)
                    {
                        // When the revision is a deletion, delete the current document
                        _database.Delete(currentKey, null, transactionInformation);
                    }
                    else
                    {
                        // Prepare the current document metadata
                        newRevisionDoc.Metadata.Remove(TemporalMetadata.RavenDocumentTemporalDeleted);
                        newRevisionDoc.Metadata.Remove(TemporalMetadata.RavenDocumentTemporalPending);
                        newRevisionDoc.Metadata.Remove("@id");
                        temporal.Status = TemporalStatus.Current;
                        temporal.RevisionNumber = int.Parse(newRevisionDoc.Key.Split('/').Last());

                        // Copy the revision to the current document
                        _database.Put(currentKey, null, newRevisionDoc.DataAsJson, newRevisionDoc.Metadata, transactionInformation);
                    }

                    // Commit the transaction
                    _database.Commit(transactionInformation.Id);
                }
            }
        }
    }
}
