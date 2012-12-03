using System.ComponentModel.Composition.Hosting;
using Raven.Bundles.TemporalVersioning.Data;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Embedded;
using Raven.Tests.Helpers;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public static class Extensions
    {
        public static EmbeddableDocumentStore GetTemporalDocumentStore(this RavenTestBase testclass)
        {
            var documentStore = new EmbeddableDocumentStore { RunInMemory = true };
            documentStore.Configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(TemporalVersioningConfiguration).Assembly));
            documentStore.Initialize();

            // Enable temporal versioning by default
            documentStore.SetTemporalVersioningEnabled(true);
            
            return documentStore;
        }
    }
}
