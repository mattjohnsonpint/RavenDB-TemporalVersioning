using System;
using System.Threading;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;
using Raven.Client.Listeners;
using Raven.Json.Linq;

namespace Raven.Client.Bundles.TemporalVersioning
{
    internal class TemporalDeleteListener : IDocumentDeleteListener
    {
        private static readonly ThreadLocal<IDocumentSession> ThreadLocalSession = new ThreadLocal<IDocumentSession>();

        internal TemporalDeleteListener(DocumentStoreBase documentStore)
        {
            documentStore.SessionCreatedInternal += session =>
                {
                    ThreadLocalSession.Value = (IDocumentSession) session;
                };
        }

        public void BeforeDelete(string key, object entityInstance, RavenJObject metadata)
        {
            var temporal = metadata.GetTemporalMetadata();
            if (temporal.Status == TemporalStatus.NonTemporal)
                return;

            if (!temporal.Effective.HasValue)
                throw new InvalidOperationException(
                    "No effective date was set for the temporal delete operation.  Be sure to specify an effective date when loading the entity to be deleted.");

            var session = (DocumentSession) ThreadLocalSession.Value;
            var headers = session.DatabaseCommands.OperationsHeaders;
            var header = string.Format("{0}-{1}", TemporalConstants.EffectiveDateHeader, key.Replace('/', '-'));
            headers[header] = temporal.Effective.Value.ToString("o");
        }
    }
}
