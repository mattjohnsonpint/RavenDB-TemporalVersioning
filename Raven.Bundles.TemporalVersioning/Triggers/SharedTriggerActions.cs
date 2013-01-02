using System.Linq;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Logging;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    internal static class SharedTriggerActions
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        public static int PutRevision(this DocumentDatabase database, string key, RavenJObject document, RavenJObject metadata, TransactionInformation transactionInformation, bool deleted = false)
        {
            Log.Debug("Putting new temporal revision for {0}", key);

            // The revision is a copy of the document.
            var revisionDocument = new RavenJObject(document);
            var revisionMetadata = new RavenJObject(metadata);

            // Set metadata on the revision
            var temporal = revisionMetadata.GetTemporalMetadata();
            var effective = temporal.EffectiveStart.GetValueOrDefault();
            temporal.Status = TemporalStatus.Revision;
            temporal.Deleted = deleted;
            temporal.Pending = effective > SystemTime.UtcNow;

            // Store the revision
            var newRevisionDoc = database.Put(key + TemporalConstants.TemporalKeySeparator, null,
                                              revisionDocument, revisionMetadata,
                                              transactionInformation);

            // Get the revision number that was generated
            var revisionNumber = int.Parse(newRevisionDoc.Key.Split('/').Last());

            if (revisionNumber > 1)
            {
                // Clear any revisions that already exist on or after the new effective date
                var futureRevisions = TemporalRevisionsIndex.GetFutureRevisions(database, key, effective).Where(x => x != newRevisionDoc.Key);
                foreach (var revisionKey in futureRevisions)
                {
                    database.SetDocumentMetadata(revisionKey, transactionInformation,
                                                 TemporalMetadata.RavenDocumentTemporalStatus,
                                                 TemporalStatus.Artifact.ToString());
                }

                // Update the until date of the last version prior to this one
                var lastRevision = TemporalRevisionsIndex.GetLastRevision(database, key, effective);
                if (lastRevision != null)
                {
                    database.SetDocumentMetadata(lastRevision, transactionInformation,
                                                 TemporalMetadata.RavenDocumentTemporalEffectiveUntil,
                                                 effective);
                }
            }

            // Reset the activation timer with each put.
            // This is so future revisions can become current without having to constantly poll.
            database.StartupTasks.OfType<TemporalActivator>().Single().ResetTimer(effective.UtcDateTime);

            // Return the revision number
            return revisionNumber;
        }
    }
}
