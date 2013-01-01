using System;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class DeletionTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_OneDelete()
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

                // Delete the document
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate2).Load<Employee>(id);
                    session.Delete(employee);

                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Null(current);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(2, revisions.Length);

                    Assert.Equal(id, revisions[0].Id);
                    Assert.Equal(id, revisions[1].Id);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate2, version1Temporal.EffectiveUntil);
                    Assert.Equal(1, version1Temporal.RevisionNumber);

                    var version2Temporal = session.Advanced.GetTemporalMetadataFor(revisions[1]);
                    Assert.Equal(TemporalStatus.Revision, version2Temporal.Status);
                    Assert.True(version2Temporal.Deleted);
                    Assert.Equal(effectiveDate2, version2Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version2Temporal.EffectiveUntil);
                    Assert.Equal(2, version2Temporal.RevisionNumber);
                }
            }
        }
    }
}
