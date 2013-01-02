using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Threading;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Plugins;
using Raven.Database.Server;

namespace Raven.Bundles.TemporalVersioning.Triggers
{
    [ExportMetadata("Bundle", TemporalConstants.BundleName)]
    public class TemporalVersioningDeleteTrigger : AbstractDeleteTrigger
    {
        private readonly ThreadLocal<JsonDocument> _originalDocument = new ThreadLocal<JsonDocument>();

        public override VetoResult AllowDelete(string key, TransactionInformation transactionInformation)
        {
            // always reset this
            _originalDocument.Value = null;

            using (Database.DisableAllTriggersForCurrentThread())
            {
                // Load the document we're going to be deleting
                var document = Database.Get(key, transactionInformation);
                if (document == null)
                    return VetoResult.Allowed;

                // Don't do anything if temporal versioning is inactive for this document type
                if (!Database.IsTemporalVersioningEnabled(key, document.Metadata))
                    return VetoResult.Allowed;

                // We shouldn't be deleting revisions directly.
                if (key.Contains(TemporalConstants.TemporalKeySeparator))
                    return VetoResult.Deny("Deleting an existing temporal revision directly is not allowed.");

                // Get the effective date of the delete from a header since we couldn't pass metadata for the delete.
                DateTimeOffset effectiveDate;
                var header = string.Format("{0}-{1}", TemporalMetadata.TemporalEffectiveDate, key.Replace('/', '-'));
                var headerValue = CurrentOperationContext.Headers.Value[header];
                if (headerValue == null || !DateTimeOffset.TryParse(headerValue, null, DateTimeStyles.RoundtripKind, out effectiveDate))
                    return VetoResult.Deny("When deleting a temporal revision, the effective date must be passed in a header.");
	            
                // The deleted revision will be effective forever
                var temporal = document.Metadata.GetTemporalMetadata();
                temporal.EffectiveStart = effectiveDate;
                temporal.EffectiveUntil = DateTimeOffset.MaxValue;

                // Put the deleted revision
                Database.PutRevision(key, document.DataAsJson, document.Metadata, transactionInformation, deleted: true);

                // If we are deleting at some future date, then hold on to a copy so we can restore it after it gets deleted.
                // (This would not be necessary if Raven supported "instead of" triggers.)
                if (temporal.EffectiveStart > SystemTime.UtcNow)
                    _originalDocument.Value = document;

                return VetoResult.Allowed;
            }
        }

        public override void AfterDelete(string key, TransactionInformation transactionInformation)
        {
            // Restore the original document when the one we just deleted was not current.
            var doc = _originalDocument.Value;
            if (doc != null)
                using (Database.DisableAllTriggersForCurrentThread())
                    Database.Put(key, null, doc.DataAsJson, doc.Metadata, transactionInformation);
        }
    }
}
