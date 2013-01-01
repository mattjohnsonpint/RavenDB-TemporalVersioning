using System;
using System.Linq;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Indexes;
using Raven.Client;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class StaticQueryTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_StaticQuery()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Employees_ByName());
                documentStore.ExecuteIndex(new Employees_CurrentByName());
                documentStore.ExecuteIndex(new Employees_RevisionsByName());

                // Store a document
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

                // Query against a current-data-only index and check the results
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.Query<Employee, Employees_CurrentByName>()
                                           .Customize(x => x.WaitForNonStaleResults().DisableTemporalFiltering())
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    Assert.Equal(1, employees.Count);
                    var employee = employees.Single();
                    Assert.Equal(20, employee.PayRate);

                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Current, temporal.Status);
                }

                //WaitForUserToContinueTheTest(documentStore);

                // Query against a revisions index without temporal filtering and check the results
                using (var session = documentStore.OpenSession())
                {
                    var revisions = session.Query<Employee, Employees_RevisionsByName>()
                                           .Customize(x => x.WaitForNonStaleResults().DisableTemporalFiltering())
                                           .OrderBy("Effective")
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    Assert.Equal(2, revisions.Count);

                    var revision1 = revisions[0];
                    var metadata1 = session.Advanced.GetMetadataFor(revision1);
                    var temporal1 = metadata1.GetTemporalMetadata();

                    Assert.Equal(id, revision1.Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, metadata1.Value<string>("@id"));
                    Assert.Equal(10, revision1.PayRate);
                    Assert.Equal(TemporalStatus.Revision, temporal1.Status);
                    Assert.Equal(1, temporal1.RevisionNumber);

                    var revision2 = revisions[1];
                    var metadata2 = session.Advanced.GetMetadataFor(revision2);
                    var temporal2 = metadata2.GetTemporalMetadata();

                    Assert.Equal(id, revision2.Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 2, metadata2.Value<string>("@id"));
                    Assert.Equal(20, revision2.PayRate);
                    Assert.Equal(TemporalStatus.Revision, temporal2.Status);
                    Assert.Equal(2, temporal2.RevisionNumber);
                }

                // Query against an unfiltered index and check the results at date 1
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.Effective(effectiveDate1)
                                           .Query<Employee, Employees_ByName>()
                                           .Customize(x => x.WaitForNonStaleResults())
                                           .Where(x => x.Name == "John")
                                           .ToList();

                    var employee = employees.Single();

                    Assert.Equal(id, employee.Id);
                    Assert.Equal(10, employee.PayRate);
                    var temporal = session.Advanced.GetTemporalMetadataFor(employee);
                    Assert.Equal(TemporalStatus.Revision, temporal.Status);
                    Assert.Equal(1, temporal.RevisionNumber);
                }

                // Query against an unfiltered and check the results at date 2
                using (var session = documentStore.OpenSession())
                {
                    var employees = session.Effective(effectiveDate2)
                                           .Query<Employee, Employees_ByName>()
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
