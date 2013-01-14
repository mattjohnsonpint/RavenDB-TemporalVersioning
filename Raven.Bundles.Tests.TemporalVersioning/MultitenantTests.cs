using System;
using System.IO;
using System.Linq;
using Raven.Bundles.TemporalVersioning;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Extensions;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class MultitenantTests : RavenTestBase
    {
        private readonly string _pluginsDirectory;

        public MultitenantTests()
        {
            // Copy the dll to a custom plugins directory.  The path is set in the tenant db settings.
            // TODO: This is messy, but seems to be neccessary.  Is there an alternative?

            var bundleAssembly = typeof(TemporalActivator).Assembly;
            var filename = Path.GetFileNameWithoutExtension(bundleAssembly.Location);
            var basedir = AppDomain.CurrentDomain.BaseDirectory + @"\";
            _pluginsDirectory = basedir + "TenantPlugins";
            if (!Directory.Exists(_pluginsDirectory))
                Directory.CreateDirectory(_pluginsDirectory);
            File.Copy(basedir + filename + ".dll", _pluginsDirectory + @"\" + filename + ".dll", true);
            File.Copy(basedir + filename + ".pdb", _pluginsDirectory + @"\" + filename + ".pdb", true);
        }

        [Fact]
        public void TemporalVersioning_Can_Coexist_With_NonTV()
        {
            using (var documentStore = NewRemoteDocumentStore().InitializeTemporalVersioning())
            {
                const string nonTemporalDbName = "NonTemporalTenantDatabase";
                documentStore.DatabaseCommands.EnsureDatabaseExists(nonTemporalDbName);

                const string temporalDbName = "TemporalTenantDatabase";
                documentStore.DatabaseCommands.EnsureDatabaseExists(temporalDbName);
                documentStore.SetTenantDatabaseSetting(temporalDbName, "Raven/PluginsDirectory", _pluginsDirectory);
                documentStore.ActivateTemporalVersioningBundle(temporalDbName);
                using (var session = documentStore.OpenSession(temporalDbName))
                {
                    session.Advanced.ConfigureTemporalVersioning<Employee>(true);
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession(nonTemporalDbName))
                {
                    session.Store(new Employee { Name = "Alice" });
                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession(temporalDbName))
                {
                    session.Store(new Employee { Name = "Bob" });
                    session.SaveChanges();
                }

                WaitForUserToContinueTheTest();

                using (var session = documentStore.OpenSession(nonTemporalDbName))
                {
                    var results = session.Query<Employee>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Customize(x => x.DisableTemporalFiltering())
                                         .ToList();

                    Assert.Equal(1, results.Count);
                }

                using (var session = documentStore.OpenSession(temporalDbName))
                {
                    var results = session.Query<Employee>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Customize(x => x.DisableTemporalFiltering())
                                         .ToList();

                    Assert.Equal(2, results.Count);
                }
            }
        }
    }
}
