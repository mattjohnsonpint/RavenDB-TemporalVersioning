using System;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Document;
using Raven.Client.Embedded;
using Raven.Client.Linq;
using Raven.Tests.Helpers;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public static class Extensions
    {
        public static EmbeddableDocumentStore GetTemporalDocumentStore(this RavenTestBase testclass)
        {
            var documentStore = new EmbeddableDocumentStore { RunInMemory = true };
            documentStore.Configuration.RegisterTemporalVersioningBundle();
            documentStore.Initialize();
            documentStore.InitializeTemporalVersioning();

            using (var session = documentStore.OpenSession())
            {
                // Enable temporal versioning for the test entities that are temporal.
                session.Advanced.ConfigureTemporalVersioning<Employee>(true);
                session.SaveChanges();
            }

            return documentStore;
        }

        public static IRavenQueryable<T> OrderBy<T>(this IRavenQueryable<T> source, params string[] fields)
        {
            return source.Customize(x => ((IDocumentQuery<T>) x).OrderBy(fields));
        }

        public static void SetTenantDatabaseSetting(this IDocumentStore documentStore, string databaseName, string key, string value)
        {
            if (!(documentStore is DocumentStore))
                throw new InvalidOperationException("Embedded databases cannot use this method.");

            using (var session = documentStore.OpenSession())
            {
                var databaseDocument = session.Load<DatabaseDocument>("Raven/Databases/" + databaseName);
                var settings = databaseDocument.Settings;

                if (settings.ContainsKey(key))
                    settings[key] = value;
                else
                    settings.Add(key, value);

                session.SaveChanges();
            }
        }
    }
}
