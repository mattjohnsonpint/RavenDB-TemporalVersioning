using System;
using System.Linq;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;

namespace Raven.Client.Bundles.TemporalVersioning
{
    public static class TemporalExtensions
    {
        /// <summary>
        /// Configures temporal versioning for all documents that aren't configured separately.
        /// </summary>
        public static void ConfigureTemporalVersioningDefaults(this IDocumentSession session, bool enabled)
        {
            session.ConfigureTemporalVersioning(enabled, "DefaultConfiguration");
        }

        /// <summary>
        /// Configures temporal versioning for an individual document type.
        /// </summary>
        public static void ConfigureTemporalVersioning<T>(this IDocumentSession session, bool enabled)
        {
            session.ConfigureTemporalVersioning(enabled, typeof(T));
        }

        /// <summary>
        /// Configures temporal versioning for an individual document type.
        /// </summary>
        public static void ConfigureTemporalVersioning(this IDocumentSession session, bool enabled, Type documentType)
        {
            var entityName = session.Advanced.DocumentStore.Conventions.GetTypeTagName(documentType);
            session.ConfigureTemporalVersioning(enabled, entityName);
        }

        private static void ConfigureTemporalVersioning(this IDocumentSession session, bool enabled, string entityName)
        {
            session.Store(new
                {
                    Id = String.Format("Raven/{0}/{1}", TemporalConstants.BundleName, entityName),
                    Enabled = enabled
                });
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

        public static void PrepareNewRevision(this IDocumentSession session, object entity)
        {
            session.PrepareNewRevision(entity, DateTimeOffset.UtcNow);
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

        public static void ActivateTemporalVersioningBundle(this IDocumentStore documentStore, string databaseName = null)
        {
            documentStore.ActivateBundle(databaseName, TemporalConstants.BundleName);
        }

        private static void ActivateBundle(this IDocumentStore documentStore, string databaseName, string bundleName)
        {
            using (var sesion = documentStore.OpenSession())
            {
                var databaseDocument = sesion.Load<DatabaseDocument>("Raven/Databases/" + databaseName);
                var settings = databaseDocument.Settings;
                var activeBundles = settings.ContainsKey(Constants.ActiveBundles) ? settings[Constants.ActiveBundles] : null;
                if (string.IsNullOrEmpty(activeBundles))
                    activeBundles = bundleName;
                else
                {
                    if (activeBundles.Split(',').Contains(bundleName, StringComparer.OrdinalIgnoreCase))
                        return;
                    activeBundles += "," + bundleName;
                }
                settings[Constants.ActiveBundles] = activeBundles;

                sesion.SaveChanges();
            }
        }
    }
}
