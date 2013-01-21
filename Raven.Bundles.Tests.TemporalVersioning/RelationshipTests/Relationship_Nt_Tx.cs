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
    public class Relationship_Nt_Tx : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Nt_Tx()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new PayChecks_ByEmployee());

                using (var session = documentStore.OpenSession())
                {
                    var effectiveDate1 = new DateTime(2012, 1, 1);
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/1", Name = "Alice Anderson", PayRate=10 });
                    session.Effective(effectiveDate1).Store(new Employee { Id = "employees/2", Name = "Bob Barker", PayRate=10 });

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    // Alice changed her last name on Feb 1, 2012
                    var effectiveDate2 = new DateTime(2012, 2, 1);
                    var employee1 = session.Effective(effectiveDate2).Load<Employee>("employees/1");
                    employee1.Name = "Alice Cooper";

                    // On the same day, Bob was given a raise
                    var employee2 = session.Effective(effectiveDate2).Load<Employee>("employees/2");
                    employee2.PayRate = 12;

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    // Issue some paychecks
                    session.Store(new PayCheck { EmployeeId = "employees/1", Issued = new DateTime(2012, 1, 15), Amount = 1000 });
                    session.Store(new PayCheck { EmployeeId = "employees/2", Issued = new DateTime(2012, 1, 15), Amount = 1000 });
                    session.Store(new PayCheck { EmployeeId = "employees/1", Issued = new DateTime(2012, 2, 15), Amount = 1000 });
                    session.Store(new PayCheck { EmployeeId = "employees/2", Issued = new DateTime(2012, 2, 15), Amount = 1200 });
                    session.SaveChanges();
                }

                // Check the Jan 15th paychecks
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<PayCheckWithEmployeeInfo, PayChecks_ByEmployee>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.Issued == new DateTime(2012, 1, 15))
                                         .OrderBy(x => x.EmployeeName)
                                         .AsProjection<PayCheckWithEmployeeInfo>()
                                         .ToList();

                    Assert.Equal(2, results.Count);

                    Assert.Equal(10, results[0].PayRate);
                    Assert.Equal(10, results[1].PayRate);

                    Assert.Equal("Alice Anderson", results[0].EmployeeName);
                    Assert.Equal("Bob Barker", results[1].EmployeeName);
                }

                // Check the Feb 15th paychecks
                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<PayCheckWithEmployeeInfo, PayChecks_ByEmployee>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.Issued == new DateTime(2012, 2, 15))
                                         .OrderBy(x => x.EmployeeName)
                                         .AsProjection<PayCheckWithEmployeeInfo>()
                                         .ToList();

                    Assert.Equal(2, results.Count);

                    Assert.Equal(10, results[0].PayRate);
                    Assert.Equal(12, results[1].PayRate);

                    Assert.Equal("Alice Cooper", results[0].EmployeeName);
                    Assert.Equal("Bob Barker", results[1].EmployeeName);
                }
            }
        }
    }
}
