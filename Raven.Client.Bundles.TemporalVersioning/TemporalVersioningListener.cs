using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;
using Raven.Client.Listeners;
using Raven.Json.Linq;

namespace Raven.Client.Bundles.TemporalVersioning
{
    internal class TemporalVersioningListener : IDocumentQueryListener, IDocumentDeleteListener, IDocumentConversionListener
    {
        private static readonly ThreadLocal<IDocumentSession> ThreadLocalSession = new ThreadLocal<IDocumentSession>();

        internal TemporalVersioningListener(DocumentStoreBase documentStore)
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

            var session = (DocumentSession) ThreadLocalSession.Value;
            var headers = session.DatabaseCommands.OperationsHeaders;
            var header = string.Format("{0}-{1}", TemporalMetadata.RavenTemporalEffective, key.Replace('/', '-'));

            var effectiveDate = temporal.Effective ?? SystemTime.UtcNow;
            headers[header] = effectiveDate.ToString("o");
        }

        public void BeforeQueryExecuted(IDocumentQueryCustomization queryCustomization)
        {
            // I hate reflection and dynamic, but this works.

            var fieldInfo = queryCustomization.GetType().GetField("includes", BindingFlags.Instance | BindingFlags.NonPublic);
            if (fieldInfo == null)
                throw new InvalidOperationException();

            string effectiveDate = null;

            var includes = (HashSet<string>) fieldInfo.GetValue(queryCustomization);
            if (includes != null)
            {
                var filteringDisabledTag = includes.FirstOrDefault(x => x == "__TemporalFilteringDisabled__");
                if (filteringDisabledTag != null)
                {
                    includes.Remove(filteringDisabledTag);
                    return;
                }

                var effectiveDateTag = includes.FirstOrDefault(x => x.StartsWith("__TemporalEffectiveDate__"));
                if (effectiveDateTag != null)
                {
                    includes.Remove(effectiveDateTag);
                    effectiveDate = effectiveDateTag.Split('=')[1];
                }
            }

            if (effectiveDate == null)
                effectiveDate = SystemTime.UtcNow.ToString("o");

            dynamic documentQuery = queryCustomization;
            var session = (DocumentSession) documentQuery.Session;
            var headers = session.DatabaseCommands.OperationsHeaders;

            headers[TemporalMetadata.RavenTemporalEffective] = effectiveDate;

            documentQuery.AfterQueryExecuted((Action<QueryResult>) (result => headers.Remove(TemporalMetadata.RavenTemporalEffective)));
        }

        public void EntityToDocument(string key, object entity, RavenJObject document, RavenJObject metadata)
        {
            if (key.Contains(TemporalConstants.TemporalKeySeparator))
                throw new InvalidOperationException("You can't save a temporal revision or artifact directly.");
        }

        public void DocumentToEntity(string key, object entity, RavenJObject document, RavenJObject metadata)
        {
            if (!key.Contains(TemporalConstants.TemporalKeySeparator))
                return;

            // When we get back a temporal revision, leave the key and the @id metadata intact,
            // But the Id property should be the base id, not the revision key.
            var baseKey = key.Substring(0, key.IndexOf(TemporalConstants.TemporalKeySeparator, StringComparison.Ordinal));

            var session = ThreadLocalSession.Value;
            var conventions = session.Advanced.DocumentStore.Conventions;
            var property = conventions.GetIdentityProperty(entity.GetType());
            if (property == null)
                return;

            property.SetValue(entity, baseKey, null);
        }
    }
}
