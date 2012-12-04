using System;
using System.Linq;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;

namespace Raven.Client.Bundles.TemporalVersioning
{
    public static class TemporalExtensions
    {
        /// <summary>
        /// Enables or disables temporal versioning for all documents that aren't configured separately.
        /// </summary>
        public static void SetTemporalVersioningEnabled(this IDocumentStore documentStore, bool enabled)
        {
            documentStore.SetTemporalVersioningEnabled(enabled, "DefaultConfiguration");
        }

        /// <summary>
        /// Enables or disables temporal versioning for an individual document type.
        /// </summary>
        public static void SetTemporalVersioningEnabled<T>(this IDocumentStore documentStore, bool enabled)
        {
            var entityName = documentStore.Conventions.GetTypeTagName(typeof(T));
            documentStore.SetTemporalVersioningEnabled(enabled, entityName);
        }

        private static void SetTemporalVersioningEnabled(this IDocumentStore documentStore, bool enabled, string entityName)
        {
            using (var session = documentStore.OpenSession())
            {
                session.Store(new
                {
                    Id = String.Format("Raven/{0}/{1}", TemporalConstants.BundleName, entityName),
                    Enabled = enabled
                });
                session.SaveChanges();
            }
        }

        public static T[] GetTemporalRevisionsFor<T>(this ISyncAdvancedSessionOperation session, string id, int start, int pageSize)
        {
            var inMemoryDocumentSessionOperations = ((InMemoryDocumentSessionOperations) session);
            var jsonDocuments = ((DocumentSession) session).DatabaseCommands.StartsWith(id + TemporalConstants.TemporalKeySeparator, null, start, pageSize);
            return jsonDocuments
                .Select(inMemoryDocumentSessionOperations.TrackEntity<T>)
                .ToArray();
        }

        public static string[] GetTemporalRevisionIdsFor(this ISyncAdvancedSessionOperation session, string id, int start, int pageSize)
        {
            var jsonDocuments = ((DocumentSession)session).DatabaseCommands.StartsWith(id + TemporalConstants.TemporalKeySeparator, null, start, pageSize, metadataOnly: true);
            return jsonDocuments
                .Select(document => document.Key)
                .ToArray();
        }

        public static void PrepareNewRevision(this IDocumentSession session, object entity, DateTimeOffset effectiveDate)
        {
            var temporal = session.Advanced.GetTemporalMetadataFor(entity);
            temporal.Status = TemporalStatus.New;
            temporal.EffectiveStart = effectiveDate;
        }

        public static ISyncTemporalSessionOperation Effective(this IDocumentSession session, DateTimeOffset effectiveDate)
        {
            return new TemporalSessionOperation(session, effectiveDate);
        }

        public static ISyncTemporalSessionOperation EffectiveNow(this IDocumentSession session)
        {
            return session.Effective(DateTimeOffset.UtcNow);
        }

        public static TemporalMetadata GetTemporalMetadataFor<T>(this ISyncAdvancedSessionOperation session, T instance)
        {
            return session.GetMetadataFor(instance).GetTemporalMetadata();
        }
    }
}
