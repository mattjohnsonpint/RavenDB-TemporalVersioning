using System;
using System.Diagnostics;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class MigrationTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Can_Migrate_From_NonTemporal_To_Temporal()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                // disable temporal versioning to start
                using (var session = documentStore.OpenSession())
                {
                    session.Advanced.ConfigureTemporalVersioning<Employee>(false);
                    session.SaveChanges();
                }

                const string id = "employees/1";
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Store(employee);
                    session.SaveChanges();
                }

                // now enable temporal versioning for the existing document
                using (var session = documentStore.OpenSession())
                {
                    session.Advanced.ConfigureTemporalVersioning<Employee>(true);
                    session.SaveChanges();
                }

                // Since we migrated a non-temporal document, and we had no original concept of
                // an effective date, we will assert that the original document was ALWAYS effective.
                var effectiveDate1 = DateTimeOffset.MinValue;

                // Check a few things
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(10, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.False(currentTemporal.Deleted);
                    Assert.Equal(effectiveDate1, currentTemporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, currentTemporal.EffectiveUntil);
                    Assert.Equal(1, currentTemporal.RevisionNumber);
                    Assert.NotNull(currentTemporal.Effective);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);

                    if (revisions.Length == 2)
                    {
                        WaitForUserToContinueTheTest(documentStore);
                    }

                    Assert.Equal(1, revisions.Length);

                    Assert.Equal(id, revisions[0].Id);
                    Assert.Equal(10, revisions[0].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version1Temporal.EffectiveUntil);
                    Assert.Equal(1, version1Temporal.RevisionNumber);
                }

                // Make some changes temporally
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate2).Load<Employee>(id);
                    employee.PayRate = 20;

                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(20, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.False(currentTemporal.Deleted);
                    Assert.Equal(effectiveDate2, currentTemporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, currentTemporal.EffectiveUntil);
                    Assert.Equal(2, currentTemporal.RevisionNumber);
                    Assert.NotNull(currentTemporal.Effective);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(2, revisions.Length);

                    Assert.Equal(id, revisions[0].Id);
                    Assert.Equal(id, revisions[1].Id);
                    Assert.Equal(10, revisions[0].PayRate);
                    Assert.Equal(20, revisions[1].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate2, version1Temporal.EffectiveUntil);
                    Assert.Equal(1, version1Temporal.RevisionNumber);

                    var version2Temporal = session.Advanced.GetTemporalMetadataFor(revisions[1]);
                    Assert.Equal(TemporalStatus.Revision, version2Temporal.Status);
                    Assert.False(version2Temporal.Deleted);
                    Assert.Equal(effectiveDate2, version2Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version2Temporal.EffectiveUntil);
                    Assert.Equal(2, version2Temporal.RevisionNumber);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_Can_Migrate_From_Temporal_To_NonTemporal()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                // Store the first version
                const string id = "employees/1";
                var effectiveDate1 = new DateTimeOffset(new DateTime(2012, 1, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);

                    session.SaveChanges();
                }

                // Make some changes temporally
                var effectiveDate2 = new DateTimeOffset(new DateTime(2012, 2, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Effective(effectiveDate2).Load<Employee>(id);
                    employee.PayRate = 20;

                    session.SaveChanges();
                }

                // Now disable temporal versioning
                using (var session = documentStore.OpenSession())
                {
                    session.Advanced.ConfigureTemporalVersioning<Employee>(false);
                    session.SaveChanges();
                }

                // Check the results.  We should have whatever was the current document.
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(20, current.PayRate);
                }
            }
        }
    }
}
