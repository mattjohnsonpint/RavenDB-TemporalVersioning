using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using System.Threading;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Logging;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Plugins;
using Raven.Database.Server;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningLoadTrigger : AbstractReadTrigger
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();

        private readonly ThreadLocal<string> _effectiveVersionKey = new ThreadLocal<string>();
        private readonly ThreadLocal<bool> _temporalVersioningEnabled = new ThreadLocal<bool>();

        public override ReadVetoResult AllowRead(string key, RavenJObject metadata, ReadOperation operation, TransactionInformation transactionInformation)
        {
            // always reset these
            _temporalVersioningEnabled.Value = false;
            _effectiveVersionKey.Value = null;

            // This trigger is only for load operations
            if (operation != ReadOperation.Load)
                return ReadVetoResult.Allowed;

            // Don't do anything if temporal versioning is inactive for this document type
            _temporalVersioningEnabled.Value = Database.IsTemporalVersioningEnabled(key, metadata);
            if (!_temporalVersioningEnabled.Value)
                return ReadVetoResult.Allowed;

            // Only operate on current temporal documents
            var temporal = metadata.GetTemporalMetadata();
            if (temporal.Status != TemporalStatus.Current)
                return ReadVetoResult.Allowed;

            // If an effective date was passed in, then use it.
            DateTime effectiveDate;
            var headerValue = CurrentOperationContext.Headers.Value[TemporalConstants.TemporalEffectiveDate];
            if (headerValue == null || !DateTime.TryParse(headerValue, null, DateTimeStyles.RoundtripKind, out effectiveDate))
            {
                // If no effective data passed, return current data, as stored, effective now.
                temporal.Effective = SystemTime.UtcNow;
                return ReadVetoResult.Allowed;
            }
            effectiveDate = DateTime.SpecifyKind(effectiveDate, DateTimeKind.Utc);

            // Return the requested effective date in the metadata.
            temporal.Effective = effectiveDate;

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
            // If we're loading a revision directly, make sure we have set the rev number in the metadata
            var temporal = metadata.GetTemporalMetadata();
            if (key != null && key.Contains(TemporalConstants.TemporalKeySeparator))
                temporal.RevisionNumber = int.Parse(key.Split('/').Last());

            // Handle migration from nontemporal document
            if (temporal.Status == TemporalStatus.NonTemporal && _temporalVersioningEnabled.Value)
            {
                // Rewrite the document temporally.  We specifically do NOT disable triggers on this put.
                temporal.Effective = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
                Database.Put(key, null, new RavenJObject(document), new RavenJObject(metadata), transactionInformation);

                // Fake out the current document for the return of this load.
                temporal.Status = TemporalStatus.Current;
                temporal.RevisionNumber = 1;
                temporal.Effective = SystemTime.UtcNow;
                temporal.EffectiveStart = DateTimeOffset.MinValue;
                temporal.EffectiveUntil = DateTimeOffset.MaxValue;
            }

            // If we didn't get a new effective version key above, just return
            var evKey = _effectiveVersionKey.Value;
            if (evKey == null)
                return;

            _log.Debug("Temporally loading {0} instead of {1}", evKey, key);

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
                {
                    if (prop != TemporalConstants.TemporalEffectiveDate)
                        metadata.Remove(prop);
                }
                var evMetadata = effectiveVersion.Metadata;
                foreach (var prop in evMetadata.Keys)
                    metadata.Add(prop, evMetadata[prop]);

                // Send back the version number also
                temporal.RevisionNumber = int.Parse(evKey.Split('/').Last());
            }
        }
    }
}
