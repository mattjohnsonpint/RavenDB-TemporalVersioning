using System;
using System.Linq;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class DynamicQueryTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_DynamicQuery()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var effectiveDate1 = new DateTimeOffset(new DateTime(2012, 1, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);
                    session.SaveChanges();
                }

                // Make some changes
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Load<Employee>(id);
                    session.PrepareNewRevision(employee, effectiveDate2);
                    employee.PayRate = 20;
                    
                    session.SaveChanges();
                }

                // Query current data and check the results
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.EffectiveNow()
                                           .Query<Employee>()
                                           .Customize(x => x.WaitForNonStaleResults())
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    Assert.Equal(1, employees.Count);
                    var employee = employees.Single();
                    Assert.Equal(20, employee.PayRate);

                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                }

                // Query non-current data and check the results at date 1
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.Effective(effectiveDate1)
                                           .Query<Employee>()
                                           .Customize(x => x.WaitForNonStaleResults())
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    Assert.Equal(1, employees.Count);
                    var employee = employees.Single();

                    Assert.Equal(id, employee.Id);
                    Assert.Equal(10, employee.PayRate);
                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                    Assert.Equal(1, temporal.RevisionNumber);
                }

                // Query non-current data and check the results at date 2
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.Effective(effectiveDate2)
                                           .Query<Employee>()
                                           .Customize(x => x.WaitForNonStaleResults())
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    var employee = employees.Single();

                    Assert.Equal(id, employee.Id);
                    Assert.Equal(20, employee.PayRate);
                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                    Assert.Equal(2, temporal.RevisionNumber);
                }
            }
        }
    }
}
