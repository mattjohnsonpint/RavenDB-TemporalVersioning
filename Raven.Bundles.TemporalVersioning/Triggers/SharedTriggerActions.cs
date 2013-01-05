using System;
using System.Collections.Generic;
using System.Linq;
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

        public static int PutRevision(this DocumentDatabase database, string key, RavenJObject document, RavenJObject metadata,
                                      TransactionInformation transactionInformation, DateTime now, bool deleted = false)
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
            temporal.Pending = effective > now;

            // Store the revision
            var newRevisionDoc = database.Put(key + TemporalConstants.TemporalKeySeparator, null,
                                              revisionDocument, revisionMetadata,
                                              transactionInformation);

            // Get the revision number that was generated
            var revisionNumber = int.Parse(newRevisionDoc.Key.Split('/').Last());

            // Get the history doc and add this revision
            Guid? historyEtag;
            var history = database.GetTemporalHistoryFor(key, transactionInformation, out historyEtag);
            history.AddRevision(newRevisionDoc.Key, temporal);

            if (revisionNumber > 1)
            {
                // Artifact any revisions that already exist on or after the new effective date
                var futureRevisions = history.Revisions.Where(x => x.Key != newRevisionDoc.Key &&
                                                                   x.Status == TemporalStatus.Revision &&
                                                                   x.EffectiveStart >= effective);
                foreach (var revisionInfo in futureRevisions)
                {
                    // in the history
                    revisionInfo.Status = TemporalStatus.Artifact;

                    // on the revision doc
                    database.SetDocumentMetadata(revisionInfo.Key, transactionInformation,
                                                 TemporalMetadata.RavenDocumentTemporalStatus,
                                                 TemporalStatus.Artifact.ToString());
                }

                // Update the until date of the last version prior to this one
                var lastRevision = history.Revisions.LastOrDefault(x => x.Key != newRevisionDoc.Key &&
                                                                        x.Status == TemporalStatus.Revision &&
                                                                        x.EffectiveStart < effective);
                if (lastRevision != null)
                {
                    // in the history
                    lastRevision.EffectiveUntil = effective;
                    lastRevision.AssertedUntil = now;

                    // on the revision doc
                    var md = new Dictionary<string, RavenJToken> {
                                                                     { TemporalMetadata.RavenDocumentTemporalEffectiveUntil, effective },
                                                                     { TemporalMetadata.RavenDocumentTemporalAssertedUntil, now }
                                                                 };
                    database.SetDocumentMetadata(lastRevision.Key, transactionInformation, md);
                }
            }

            // Update the history doc
            database.SaveTemporalHistoryFor(key, history, transactionInformation, historyEtag);

            // Reset the activation timer with each put.
            // This is so future revisions can become current without having to constantly poll.
            database.StartupTasks.OfType<TemporalActivator>().Single().ResetTimer(effective.UtcDateTime, now);

            // Return the revision number
            return revisionNumber;
        }
    }
}
