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
    public class Relationship_Nt_Tc : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Nt_Tc()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Departments_BySupervisor());

                var effectiveDate1 = new DateTimeOffset(new DateTime(2012, 1, 1));
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));

                using (var session = documentStore.OpenSession())
                {
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/1", Name = "Alice Anderson" });
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/2", Name = "Bob Barker" });

                    session.Store(new Department { Id = "departments/1", Name = "Accounting", SupervisorEmployeeId = "employees/1" });
                    session.Store(new Department { Id = "departments/2", Name = "Sales", SupervisorEmployeeId = "employees/2" });

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    // Alice changed her last name on Feb 1, 2012
                    var employee = session.Effective(effectiveDate2).Load<Employee>("employees/1");
                    employee.Name = "Alice Cooper";

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<DepartmentWithSupervisor, Departments_BySupervisor>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<DepartmentWithSupervisor>()
                                         .ToList();

                    Assert.Equal(2, results.Count);

                    Assert.Equal("Accounting", results[0].Name);
                    Assert.Equal("Sales", results[1].Name);

                    Assert.Equal("Alice Cooper", results[0].Supervisor);
                    Assert.Equal("Bob Barker", results[1].Supervisor);
                }
            }
        }
    }
}
