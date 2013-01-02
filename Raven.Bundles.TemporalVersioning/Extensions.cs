using System.ComponentModel.Composition.Hosting;
using Raven.Database.Config;

namespace Raven.Bundles.TemporalVersioning
{
    public static class Extensions
    {
        public static void RegisterTemporalVersioningBundle(this InMemoryRavenConfiguration configuration)
        {
            configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(Extensions).Assembly));
        }
    }
}
