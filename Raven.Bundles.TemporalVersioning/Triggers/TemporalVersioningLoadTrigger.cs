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
        private readonly ThreadLocal<DateTime> _now = new ThreadLocal<DateTime>();

        public override ReadVetoResult AllowRead(string key, RavenJObject metadata, ReadOperation operation, TransactionInformation transactionInformation)
        {
            // always reset these
            _temporalVersioningEnabled.Value = false;
            _effectiveVersionKey.Value = null;
            _now.Value = SystemTime.UtcNow;

            if (key == null)
                return ReadVetoResult.Allowed;

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
            DateTimeOffset effectiveDate;
            var headerValue = CurrentOperationContext.Headers.Value[TemporalMetadata.RavenTemporalEffective];
            if (headerValue == null || !DateTimeOffset.TryParse(headerValue, null, DateTimeStyles.RoundtripKind, out effectiveDate))
            {
                // If no effective data passed, return current data, as stored, effective now.
                temporal.Effective = _now.Value;
                return ReadVetoResult.Allowed;
            }

            // Return the requested effective date in the metadata.
            temporal.Effective = effectiveDate;

            // If the current document is already in range, just return it
            if (temporal.EffectiveStart <= effectiveDate && effectiveDate < temporal.EffectiveUntil)
                return ReadVetoResult.Allowed;

            // Load the history doc
            Guid? historyEtag;
            var history = Database.GetTemporalHistoryFor(key, transactionInformation, out historyEtag);

            // Find the version that is effective at the date requested
            var effectiveRevisionInfo = history.Revisions.FirstOrDefault(x => x.Status == TemporalStatus.Revision &&
                                                                              x.EffectiveStart <= effectiveDate &&
                                                                              x.EffectiveUntil > effectiveDate);
            // Return nothing if there is no revision at the effective date
            if (effectiveRevisionInfo == null)
                return ReadVetoResult.Ignore;

            // Hold on to the key so we can use it later in OnRead
            _effectiveVersionKey.Value = effectiveRevisionInfo.Key;
            return ReadVetoResult.Allowed;
        }

        public override void OnRead(string key, RavenJObject document, RavenJObject metadata, ReadOperation operation,
                                    TransactionInformation transactionInformation)
        {
            if (key == null)
                return;

            // If we're loading a revision directly, make sure we have set the rev number in the metadata
            var temporal = metadata.GetTemporalMetadata();
            if (key.Contains(TemporalConstants.TemporalKeySeparator))
                temporal.RevisionNumber = int.Parse(key.Split('/').Last());

            // Handle migration from nontemporal document
            if (temporal.Status == TemporalStatus.NonTemporal && _temporalVersioningEnabled.Value)
            {
                // Rewrite the document temporally.  We specifically do NOT disable triggers on this put.
                temporal.Effective = DateTimeOffset.MinValue;
                Database.Put(key, null, new RavenJObject(document), new RavenJObject(metadata), transactionInformation);

                // Fake out the current document for the return of this load.
                temporal.Status = TemporalStatus.Current;
                temporal.RevisionNumber = 1;
                temporal.Effective = _now.Value;
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
                    if (prop != TemporalMetadata.RavenTemporalEffective)
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
