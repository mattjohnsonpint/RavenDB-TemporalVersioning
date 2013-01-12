using System;
using System.Linq;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Client.Listeners;

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

        /// <summary>
        /// Gets a document containing all of the temporal metadata for every revision of a document.
        /// </summary>
        /// <param name="session">The advanced session.</param>
        /// <param name="id">The non-temporal document id.</param>
        /// <returns>A TemporalHistory document.</returns>
        public static TemporalHistory GetTemporalHistoryFor(this IAdvancedDocumentSessionOperations session, string id)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentNullException("id");

            if (id.IndexOf(TemporalConstants.TemporalKeySeparator, StringComparison.OrdinalIgnoreCase) != -1)
                throw new ArgumentException("Pass the non-temporal id, not a temporal revisions key.");

            if (id.StartsWith("Raven/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Raven system docs can not be versioned.");

            var key = TemporalHistory.GetKeyFor(id);
            var history = ((IDocumentSession)session).Load<TemporalHistory>(key);
            if (history != null)
            {
                // don't track this in the session
                session.Evict(history);
            }

            return history;
        }

        public static ISyncTemporalSessionOperation Effective(this IDocumentSession session, DateTimeOffset effective)
        {
            return new TemporalSessionOperation(session, effective);
        }

        public static IDocumentQueryCustomization DisableTemporalFiltering(this IDocumentQueryCustomization customization)
        {
            // this gets stripped out later by the listener
            return customization.Include("__TemporalFilteringDisabled__");
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
                    if (activeBundles.Split(';').Contains(bundleName, StringComparer.OrdinalIgnoreCase))
                        return;
                    activeBundles += ";" + bundleName;
                }
                settings[Constants.ActiveBundles] = activeBundles;

                session.SaveChanges();
            }
        }

        public static void InitializeTemporalVersioning(this DocumentStoreBase documentStore)
        {
            var listener = new TemporalVersioningListener(documentStore);
            documentStore.RegisterListener((IDocumentQueryListener) listener);
            documentStore.RegisterListener((IDocumentDeleteListener) listener);
            documentStore.RegisterListener((IDocumentConversionListener) listener);
        }
    }
}
