using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;
using Raven.Client.Listeners;
using Raven.Json.Linq;

namespace Raven.Client.Bundles.TemporalVersioning
{
    internal class TemporalDeleteListener : IDocumentDeleteListener
    {
        private static readonly object Padlock = new object();
        private static readonly FieldInfo DocumentStoreListenersFieldInfo;

        static TemporalDeleteListener()
        {
            // gee I wish this was public!
            DocumentStoreListenersFieldInfo = typeof(DocumentStoreBase).GetField("listeners", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        internal static void Register(IDocumentSession session)
        {
            ThreadLocalSession.Value = session;
            EnsureRegistered((DocumentStoreBase) session.Advanced.DocumentStore);
        }

        private static void EnsureRegistered(DocumentStoreBase documentStore)
        {
            lock (Padlock)
            {
                var listeners = (DocumentSessionListeners) DocumentStoreListenersFieldInfo.GetValue(documentStore);
                if (!listeners.DeleteListeners.OfType<TemporalDeleteListener>().Any())
                    documentStore.RegisterListener(new TemporalDeleteListener());
            }
        }

        private TemporalDeleteListener() { }

        private static readonly ThreadLocal<IDocumentSession> ThreadLocalSession = new ThreadLocal<IDocumentSession>();

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
