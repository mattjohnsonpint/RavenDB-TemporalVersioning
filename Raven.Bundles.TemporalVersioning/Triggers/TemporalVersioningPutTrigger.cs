using System;
using System.ComponentModel.Composition;
using System.Threading;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Plugins;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningPutTrigger : AbstractPutTrigger
    {
        private readonly ThreadLocal<bool> _clearCurrent = new ThreadLocal<bool>();
        private readonly ThreadLocal<JsonDocument> _originalDocument = new ThreadLocal<JsonDocument>();

        public override VetoResult AllowPut(string key, RavenJObject document, RavenJObject metadata, TransactionInformation transactionInformation)
        {
            // always reset these
            _clearCurrent.Value = false;
            _originalDocument.Value = null;

            if (key == null)
                return VetoResult.Allowed;

            // Don't do anything if temporal versioning is inactive for this document type
            if (!Database.IsTemporalVersioningEnabled(key, metadata))
                return VetoResult.Allowed;

            // Don't allow modifications to revision documents
            if (key.Contains(TemporalConstants.TemporalKeySeparator))
                return VetoResult.Deny("Modifying an existing temporal revision directly is not allowed.");

            // If no effective date was passed in, use now.
            var temporal = metadata.GetTemporalMetadata();
            if (!temporal.Effective.HasValue)
                temporal.Effective = SystemTime.UtcNow;

            return VetoResult.Allowed;
        }

        public override void OnPut(string key, RavenJObject document, RavenJObject metadata, TransactionInformation transactionInformation)
        {
            if (key == null)
                return;

            if (key.StartsWith("Raven/" + TemporalConstants.BundleName + "/Raven/"))
                throw new InvalidOperationException("Cannot version RavenDB system documents!");

            // Clear the config cache any time a new configuration is written.
            if (key.StartsWith("Raven/" + TemporalConstants.BundleName + "/"))
                TemporalVersioningUtil.ConfigCache.Clear();

            if (!Database.IsTemporalVersioningEnabled(key, metadata))
                return;

            using (Database.DisableAllTriggersForCurrentThread())
            {
                // When it's initially written, data is considered effective from the date specified to the end of time.
                var temporal = metadata.GetTemporalMetadata();
                temporal.EffectiveStart = temporal.Effective;
                temporal.EffectiveUntil = DateTimeOffset.MaxValue;

                // See if the revision we're saving is current.
                var now = SystemTime.UtcNow;
                var current = temporal.Effective <= now;

                // Don't save the requested date with the document
                temporal.Effective = null;

                if (!current)
                {
                    // When it's not current, then fetch the current one so we can put it back later.
                    // (This would not be necessary if Raven supported "instead of" triggers.)
                    _originalDocument.Value = Database.Get(key, transactionInformation);

                    // If this is the first revision and it's not current, then we don't want to keep a current doc at all.
                    _clearCurrent.Value = _originalDocument.Value == null;
                }

                // Always store this new data as a revision document
                var versionNumber = Database.PutRevision(key, document, metadata, transactionInformation);

                if (current)
                {
                    // When it's current, set the appropriate values the document that will be stored
                    temporal.Status = TemporalStatus.Current;
                    temporal.RevisionNumber = versionNumber;
                }
            }
        }

        public override void AfterPut(string key, RavenJObject document, RavenJObject metadata, Guid etag, TransactionInformation transactionInformation)
        {
            if (key == null)
                return;

            using (Database.DisableAllTriggersForCurrentThread())
            {
                // Restore the original document when the one we just saved is not current.
                var doc = _originalDocument.Value;
                if (doc != null)
                    Database.Put(key, null, doc.DataAsJson, doc.Metadata, transactionInformation);

                // If there was no prior current document, then delete this one if it's not current either.
                if (_clearCurrent.Value)
                    Database.Delete(key, null, transactionInformation);
            }
        }
    }
}
