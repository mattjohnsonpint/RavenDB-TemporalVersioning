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
    [InheritedExport(typeof(AbstractPutTrigger))]
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningPutTrigger : AbstractPutTrigger
    {
        private readonly ThreadLocal<bool> _clearCurrent = new ThreadLocal<bool>();
        private readonly ThreadLocal<JsonDocument> _originalDocument = new ThreadLocal<JsonDocument>();
        private readonly ThreadLocal<DateTime> _now = new ThreadLocal<DateTime>(); 

        public override VetoResult AllowPut(string key, RavenJObject document, RavenJObject metadata, TransactionInformation transactionInformation)
        {
            // always reset these
            _clearCurrent.Value = false;
            _originalDocument.Value = null;
            _now.Value = SystemTime.UtcNow;

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
                temporal.Effective = _now.Value;

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

                // Set the asserted dates
                temporal.AssertedStart = _now.Value;
                temporal.AssertedUntil = DateTimeOffset.MaxValue;

                // See if the revision we're saving is current.
                var current = temporal.Effective <= _now.Value;

                // Don't save the requested date with the document
                temporal.Effective = null;
                
                if (!current)
                {
                    // When it's not current, then fetch the current one so we can put it back later.
                    _originalDocument.Value = Database.Get(key, transactionInformation);

                    // If this is the first revision and it's not current, then we don't want to keep a current doc at all.
                    _clearCurrent.Value = _originalDocument.Value == null;

                    // If this is a future version, then the current doc might need to be updated
                    if (_originalDocument.Value != null)
                    {
                        var originalMetadata = _originalDocument.Value.Metadata.GetTemporalMetadata();
                        if (originalMetadata.EffectiveUntil > temporal.EffectiveStart)
                        {
                            originalMetadata.EffectiveUntil = temporal.EffectiveStart;
                            originalMetadata.AssertedUntil = _now.Value;
                        }
                    }
                }

                // Always store this new data as a revision document
                var versionNumber = Database.PutRevision(key, document, metadata, transactionInformation, _now.Value);

                if (current)
                {
                    // When it's current, set the appropriate values the document that will be stored
                    temporal.Status = TemporalStatus.Current;
                    temporal.RevisionNumber = versionNumber;
                }
            }
        }

        public override void AfterPut(string key, RavenJObject document, RavenJObject metadata, Etag etag, TransactionInformation transactionInformation)
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
