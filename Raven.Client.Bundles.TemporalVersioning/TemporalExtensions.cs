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
        public static void ConfigureTemporalVersioningDefaults(this IAdvancedDocumentSessionOperations session, bool enabled)
        {
            session.ConfigureTemporalVersioning(enabled, "DefaultConfiguration");
        }

        /// <summary>
        /// Configures temporal versioning for an individual document type.
        /// </summary>
        public static void ConfigureTemporalVersioning<T>(this IAdvancedDocumentSessionOperations session, bool enabled)
        {
            session.ConfigureTemporalVersioning(enabled, typeof(T));
        }

        /// <summary>
        /// Configures temporal versioning for an individual document type.
        /// </summary>
        public static void ConfigureTemporalVersioning(this IAdvancedDocumentSessionOperations session, bool enabled, Type documentType)
        {
            var entityName = session.DocumentStore.Conventions.GetTypeTagName(documentType);
            session.ConfigureTemporalVersioning(enabled, entityName);
        }

        private static void ConfigureTemporalVersioning(this IAdvancedDocumentSessionOperations session, bool enabled, string entityName)
        {
            var inMemoryDocumentSessionOperations = ((InMemoryDocumentSessionOperations) session);
            var configuration = new TemporalVersioningConfiguration {
                                                                        Id = String.Format("Raven/{0}/{1}", TemporalConstants.BundleName, entityName),
                                                                        Enabled = enabled
                                                                    };
            inMemoryDocumentSessionOperations.Store(configuration);
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
            var jsonDocuments = ((DocumentSession) session).DatabaseCommands.StartsWith(id + TemporalConstants.TemporalKeySeparator, null, start, pageSize,
                                                                                        metadataOnly: true);
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

        /// <summary>
        /// Activates the TemporalVersioning bundle on a tenant database by modifying the Raven/ActiveBundles setting.
        /// </summary>
        /// <param name="documentStore">The document store instance.</param>
        /// <param name="databaseName">The name of the tenant database to activate on.</param>
        public static void ActivateTemporalVersioningBundle(this IDocumentStore documentStore, string databaseName)
        {
            documentStore.ActivateBundle(databaseName, TemporalConstants.BundleName);
        }

        private static void ActivateBundle(this IDocumentStore documentStore, string databaseName, string bundleName)
        {
            if (!(documentStore is DocumentStore))
                throw new InvalidOperationException("Embedded databases cannot use this method.  Add the bundle assembly to the configuration catalog instead.");

            using (var session = documentStore.OpenSession())
            {
                var databaseDocument = session.Load<DatabaseDocument>("Raven/Databases/" + databaseName);
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

                session.SaveChanges();
            }
        }
    }
}
