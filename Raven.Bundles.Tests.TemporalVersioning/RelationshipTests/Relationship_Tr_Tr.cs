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
    public class Relationship_Tr_Tr : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Tr_Tr()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Employees_ByManager());

                var effectiveDate1 = new DateTime(2012, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                var effectiveDate2 = new DateTime(2012, 2, 1, 0, 0, 0, DateTimeKind.Utc);
                var effectiveDate3 = new DateTime(2012, 3, 1, 0, 0, 0, DateTimeKind.Utc);

                using (var session = documentStore.OpenSession())
                {
                    // Alice manages both Bob and Charlie
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/1", Name = "Alice Anderson" });
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/2", Name = "Bob Barker", ManagerId = "employees/1" });
                    session.Effective(effectiveDate1)
                           .Store(new Employee { Id = "employees/3", Name = "Charlie Chaplin", ManagerId = "employees/1" });

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

                using (var session = documentStore.OpenSession())
                {
                    // Charlie changed his name to Mary on Mar 1, 2012.  (Crazier things have happened.)
                    var employee3 = session.Effective(effectiveDate3).Load<Employee>("employees/3");
                    employee3.Name = "Mary Chaplin";
                    
                    session.SaveChanges();
                }

                // Check the results as of the first date
                using (var session = documentStore.OpenSession())
                {
                    // Note that we must be explicit about the effective date of the manager in a separate where clause.
                    // The original effective date only filters the employee - not the manager.
                    var results = session.Effective(effectiveDate1)
                                         .Query<EmployeeWithManager_OverRange, Employees_ByManager>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.ManagerEffectiveStart <= effectiveDate1 && x.ManagerEffectiveUntil > effectiveDate1)
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithManager>()
                                         .ToList();
                    
                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice Anderson", results[0].Name);
                    Assert.Equal("Bob Barker", results[1].Name);
                    Assert.Equal("Charlie Chaplin", results[2].Name);

                    Assert.Null(results[0].Manager);
                    Assert.Equal("Alice Anderson", results[1].Manager);
                    Assert.Equal("Alice Anderson", results[2].Manager);
                }

                // Check the results as of the second date
                using (var session = documentStore.OpenSession())
                {
                    // Note that we must be explicit about the effective date of the manager in a separate where clause.
                    // The original effective date only filters the employee - not the manager.
                    var results = session.Effective(effectiveDate2)
                                         .Query<EmployeeWithManager_OverRange, Employees_ByManager>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.ManagerEffectiveStart <= effectiveDate2 && x.ManagerEffectiveUntil > effectiveDate2)
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithManager>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice Cooper", results[0].Name);
                    Assert.Equal("Bob Barker", results[1].Name);
                    Assert.Equal("Charlie Chaplin", results[2].Name);

                    Assert.Null(results[0].Manager);
                    Assert.Equal("Charlie Chaplin", results[1].Manager);
                    Assert.Equal("Alice Cooper", results[2].Manager);
                }

                // Check the results as of the third date
                using (var session = documentStore.OpenSession())
                {
                    // Note that we must be explicit about the effective date of the manager in a separate where clause.
                    // The original effective date only filters the employee - not the manager.
                    var results = session.Effective(effectiveDate3)
                                         .Query<EmployeeWithManager_OverRange, Employees_ByManager>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.ManagerEffectiveStart <= effectiveDate3 && x.ManagerEffectiveUntil > effectiveDate3)
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithManager>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice Cooper", results[0].Name);
                    Assert.Equal("Bob Barker", results[1].Name);
                    Assert.Equal("Mary Chaplin", results[2].Name);

                    Assert.Null(results[0].Manager);
                    Assert.Equal("Mary Chaplin", results[1].Manager);
                    Assert.Equal("Alice Cooper", results[2].Manager);
                }
            }
        }
    }
}
