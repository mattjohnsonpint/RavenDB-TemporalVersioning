using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Plugins;
using Raven.Database.Server;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningLoadTrigger : AbstractReadTrigger
    {
        private readonly ThreadLocal<string> _effectiveVersionKey = new ThreadLocal<string>();

        public override ReadVetoResult AllowRead(string key, RavenJObject metadata, ReadOperation operation, TransactionInformation transactionInformation)
        {
            // always reset this
            _effectiveVersionKey.Value = null;

            // This trigger is only for load operations
            if (operation != ReadOperation.Load)
                return ReadVetoResult.Allowed;

            // Don't do anything if temporal versioning is inactive for this document type
            if (!Database.IsTemporalVersioningEnabled(key, metadata))
                return ReadVetoResult.Allowed;

            // Only operate on current temporal documents
            var temporal = metadata.GetTemporalMetadata();
            if (temporal.Status != TemporalStatus.Current)
                return ReadVetoResult.Allowed;

            // If an effective date was passed in, then use it.
            DateTimeOffset effectiveDate;
            var headerValue = CurrentOperationContext.Headers.Value[TemporalConstants.EffectiveDateHeader];
            if (headerValue == null || !DateTimeOffset.TryParse(headerValue, out effectiveDate))
            {
                // If no effective data passed, return as stored.
                return ReadVetoResult.Allowed;
            }

            // If the current document is already in range, just return it
            if (temporal.EffectiveStart <= effectiveDate && effectiveDate < temporal.EffectiveUntil)
                return ReadVetoResult.Allowed;
            
            // Now we have to go find the active revision.
            using (Database.DisableAllTriggersForCurrentThread())
            {
                // Find the version that is effective at the date requested
                var evKey = TemporalRevisionsIndex.GetActiveRevision(Database, key, effectiveDate);

                // Make sure we got something
                if (evKey != null)
                {
                    // Hold on to the key so we can use it later in OnRead
                    _effectiveVersionKey.Value = evKey;
                    return ReadVetoResult.Allowed;
                }
            }

            // There is no revision at the effective date
            return ReadVetoResult.Ignore;
        }

        public override void OnRead(string key, RavenJObject document, RavenJObject metadata, ReadOperation operation,
                                    TransactionInformation transactionInformation)
        {
            // If we didn't get a new effective version key above, just return
            var evKey = _effectiveVersionKey.Value;
            if (evKey == null)
                return;

            using (Database.DisableAllTriggersForCurrentThread())
            {
                // Load the effective document
                var effectiveVersion = Database.Get(evKey, transactionInformation);

                // Replace the resulting document
                foreach (var prop in document.Keys)
                    document.Remove(prop);
                var evDoc = effectiveVersion.DataAsJson;
                foreach (var prop in evDoc.Keys)
                    document.Add(prop, evDoc[prop]);

                // Replace the resulting metadata
                foreach (var prop in metadata.Keys)
                    metadata.Remove(prop);
                var evMetadata = effectiveVersion.Metadata;
                foreach (var prop in evMetadata.Keys)
                    metadata.Add(prop, evMetadata[prop]);

                // Send back the version number also
                var temporal = metadata.GetTemporalMetadata();
                temporal.RevisionNumber = int.Parse(evKey.Split('/').Last());
            }
        }
    }
}
