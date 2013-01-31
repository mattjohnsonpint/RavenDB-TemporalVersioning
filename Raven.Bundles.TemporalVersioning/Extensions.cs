using System;
using System.Collections.Specialized;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using Raven.Abstractions.Data;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database.Config;

namespace Raven.Bundles.TemporalVersioning
{
    public static class Extensions
    {
        public static void RegisterTemporalVersioningBundle(this InMemoryRavenConfiguration configuration)
        {
            configuration.Catalog.Catalogs.Add(new AssemblyCatalog(typeof(Extensions).Assembly));
            configuration.Settings.AddBundle(TemporalConstants.BundleName);
        }

        private static void AddBundle(this NameValueCollection settings, string bundleName)
        {
            var activeBundles = settings[Constants.ActiveBundles];
            if (string.IsNullOrEmpty(activeBundles))
            {
                settings[Constants.ActiveBundles] = bundleName;
                return;
            }

            if (!activeBundles.Split(';').Contains(bundleName, StringComparer.OrdinalIgnoreCase))
                settings[Constants.ActiveBundles] = activeBundles + ";" + bundleName;
        }
    }
}
