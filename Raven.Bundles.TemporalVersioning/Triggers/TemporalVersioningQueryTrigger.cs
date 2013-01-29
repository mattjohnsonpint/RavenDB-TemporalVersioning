using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Plugins;
using Raven.Database.Server;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    [InheritedExport(typeof(AbstractReadTrigger))]
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningQueryTrigger : AbstractReadTrigger
    {
        public override ReadVetoResult AllowRead(string key, RavenJObject metadata, ReadOperation operation, TransactionInformation transactionInformation)
        {
            // This trigger is only for simple query operations
            if (key == null || operation != ReadOperation.Query)
                return ReadVetoResult.Allowed;

            // Don't do anything if temporal versioning is inactive for this document type
            if (!Database.IsTemporalVersioningEnabled(key, metadata))
                return ReadVetoResult.Allowed;

            // If an effective date was passed in, then use it.
            DateTimeOffset effective;
            var headerValue = CurrentOperationContext.Headers.Value[TemporalMetadata.RavenTemporalEffective];
            if (headerValue == null || !DateTimeOffset.TryParse(headerValue, null, DateTimeStyles.RoundtripKind, out effective))
            {
                // If no effective data passed, return as stored.
                return ReadVetoResult.Allowed;
            }

            // Return the requested effective date in the metadata.
            var temporal = metadata.GetTemporalMetadata();
            temporal.Effective = effective;

            // Return the result if it's the active revision, or skip it otherwise.
            return temporal.Status == TemporalStatus.Revision &&
                   temporal.EffectiveStart <= effective && effective < temporal.EffectiveUntil &&
                   !temporal.Deleted
                       ? ReadVetoResult.Allowed
                       : ReadVetoResult.Ignore;
        }

        public override void OnRead(string key, RavenJObject document, RavenJObject metadata, ReadOperation operation,
                                    TransactionInformation transactionInformation)
        {
            // This trigger is only for simple query operations
            if (key == null || operation != ReadOperation.Query)
                return;

            // Don't do anything when temporal versioning is not enabled
            if (!Database.IsTemporalVersioningEnabled(key, metadata))
                return;

            // Only operate on temporal revisions
            var temporal = metadata.GetTemporalMetadata();
            if (temporal.Status != TemporalStatus.Revision)
                return;

            // Send back the revision number
            temporal.RevisionNumber = int.Parse(key.Split('/').Last());

            // When we filtered by effective date, return the document id instead of the revision id
            if (temporal.Effective.HasValue)
                metadata["@id"] = key.Substring(0, key.IndexOf(TemporalConstants.TemporalKeySeparator, StringComparison.Ordinal));
        }
    }
}
