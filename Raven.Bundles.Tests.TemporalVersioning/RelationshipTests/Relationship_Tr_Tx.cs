using System;
using System.Linq;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Indexes;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Linq;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning.RelationshipTests
{
    public class Relationship_Tr_Tx : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Tr_Tx()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Employees_ByHiringManager());

                var effectiveDate1 = new DateTime(2012, 1, 1);
                var effectiveDate2 = new DateTime(2012, 2, 1);

                using (var session = documentStore.OpenSession())
                {
                    // Alice manages both Bob and Charlie
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/1", Name = "Alice Anderson", HireDate = effectiveDate1 });
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/2", Name = "Bob Barker", ManagerId = "employees/1", HireDate = effectiveDate1 });
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/3", Name = "Charlie Chaplin", ManagerId = "employees/1", HireDate = effectiveDate1 });

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    // Alice changed her last name on Feb 1, 2012
                    var employee1 = session.Effective(effectiveDate2).Load<Employee>("employees/1");
                    employee1.Name = "Alice Cooper";

                    // On the same day, Charlie became Bob's manager
                    var employee2 = session.Effective(effectiveDate2).Load<Employee>("employees/2");
                    employee2.ManagerId = "employees/3";

                    session.SaveChanges();
                }

                // Check the results as of the first date
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Effective(effectiveDate1)
                                         .Query<EmployeeWithManager, Employees_ByHiringManager>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithManager>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice Anderson", results[0].Name);
                    Assert.Equal("Bob Barker", results[1].Name);
                    Assert.Equal("Charlie Chaplin", results[2].Name);

                    // we specifically asked for the hiring manager
                    Assert.Null(results[0].Manager);
                    Assert.Equal("Alice Anderson", results[1].Manager);
                    Assert.Equal("Alice Anderson", results[2].Manager);
                }

                // Check the results as of the second date
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Effective(effectiveDate2)
                                         .Query<EmployeeWithManager, Employees_ByHiringManager>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithManager>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice Cooper", results[0].Name);
                    Assert.Equal("Bob Barker", results[1].Name);
                    Assert.Equal("Charlie Chaplin", results[2].Name);

                    // we specifically asked for the hiring manager
                    Assert.Null(results[0].Manager);
                    Assert.Equal("Alice Anderson", results[1].Manager);
                    Assert.Equal("Alice Anderson", results[2].Manager);
                }
            }
        }
    }
}
