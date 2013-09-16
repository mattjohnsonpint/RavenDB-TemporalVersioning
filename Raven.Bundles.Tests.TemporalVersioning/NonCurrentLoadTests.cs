using System;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class NonCurrentLoadTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_NonCurrentLoad()
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
                    var employee = session.Effective(effectiveDate2).Load<Employee>(id);
                    employee.PayRate = 20;

                    session.SaveChanges();
                }

                // Load non-current data and check the results
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate1).Load<Employee>(id);
                    Assert.Equal(id, employee.Id);
                    Assert.Equal(10, employee.PayRate);

                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                    Assert.Equal(1, temporal.RevisionNumber);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_NonCurrentLoad_WithFutureEdit()
        {
            var now = DateTimeOffset.Now;

            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var effectiveDate1 = now.AddMonths(-2);
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);

                    session.SaveChanges();
                }

                // Make some changes
                var effectiveDate2 = now.AddMonths(-1);
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate2).Load<Employee>(id);
                    employee.PayRate = 20;

                    session.SaveChanges();
                }

                // Make some future changes
                var effectiveDate3 = now.AddMonths(1);
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate3).Load<Employee>(id);
                    employee.PayRate = 30;

                    session.SaveChanges();
                }

                // Check the results at the end
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate3).Load<Employee>(id);
                    Assert.Equal(id, employee.Id);
                    Assert.Equal(30, employee.PayRate);

                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                    Assert.Equal(3, temporal.RevisionNumber);
                }
            }
        }
    }
}
