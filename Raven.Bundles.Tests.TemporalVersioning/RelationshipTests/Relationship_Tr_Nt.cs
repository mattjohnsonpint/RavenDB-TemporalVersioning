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
    public class Relationship_Tr_Nt : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Tr_Nt()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Employees_ByDepartment());

                var effectiveDate1 = new DateTimeOffset(new DateTime(2012, 1, 1));
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new Department { Id = "departments/1", Name = "Accounting" });
                    session.Store(new Department { Id = "departments/2", Name = "Sales" });
                    
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/1", Name = "Alice", DepartmentId = "departments/1" });
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/2", Name = "Bob", DepartmentId = "departments/1" });
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/3", Name = "Charlie", DepartmentId = "departments/2" });

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    // Bob moved to the Sales department on Feb 1, 2012
                    var employee = session.Effective(effectiveDate2).Load<Employee>("employees/2");
                    employee.DepartmentId = "departments/2";

                    session.SaveChanges();
                }

                // Check results as of today
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<EmployeeWithDepartment, Employees_ByDepartment>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithDepartment>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice", results[0].Name);
                    Assert.Equal("Bob", results[1].Name);
                    Assert.Equal("Charlie", results[2].Name);

                    Assert.Equal("Accounting", results[0].Department);
                    Assert.Equal("Sales", results[1].Department);
                    Assert.Equal("Sales", results[2].Department);
                }

                // Check results as of January
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Effective(effectiveDate1)
                                         .Query<EmployeeWithDepartment, Employees_ByDepartment>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithDepartment>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice", results[0].Name);
                    Assert.Equal("Bob", results[1].Name);
                    Assert.Equal("Charlie", results[2].Name);

                    Assert.Equal("Accounting", results[0].Department);
                    Assert.Equal("Accounting", results[1].Department);
                    Assert.Equal("Sales", results[2].Department);
                }

                // Check results as of February
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Effective(effectiveDate2)
                                         .Query<EmployeeWithDepartment, Employees_ByDepartment>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .OrderBy(x => x.Name)
                                         .AsProjection<EmployeeWithDepartment>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Alice", results[0].Name);
                    Assert.Equal("Bob", results[1].Name);
                    Assert.Equal("Charlie", results[2].Name);

                    Assert.Equal("Accounting", results[0].Department);
                    Assert.Equal("Sales", results[1].Department);
                    Assert.Equal("Sales", results[2].Department);
                }
            }
        }
    }
}
