using System;
using System.Threading;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class SimpleTests : RavenTestBase
    {
        [Fact]
        public void CanSaveLoadDateTimeOffsetFromMetadata()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var testDateTimeOffset = new DateTimeOffset(2012, 1, 1, 8, 0, 0, TimeSpan.FromHours(-2));
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John" };
                    session.Store(employee);
                    session.Advanced.GetMetadataFor(employee).Add("TestDateTimeOffset", testDateTimeOffset);
                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    var metadataCurrent = session.Advanced.GetMetadataFor(current);
                    Assert.Equal(testDateTimeOffset, metadataCurrent.Value<DateTimeOffset>("TestDateTimeOffset"));
                }
            }
        }

        [Fact]
        public void TemporalVersioning_NoEdits()
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

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(10, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(1, currentTemporal.RevisionNumber);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(1, revisions.Length);

                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, revisions[0].Id);
                    Assert.Equal(10, revisions[0].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version1Temporal.EffectiveUntil);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_NoEdits_Future()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var effectiveDate1 = DateTimeOffset.Now.AddYears(1);
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);
                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    // there should be no current revision
                    var current = session.Load<Employee>(id);
                    Assert.Null(current);
                    
                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(1, revisions.Length);

                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, revisions[0].Id);
                    Assert.Equal(10, revisions[0].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.True(version1Temporal.Pending);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version1Temporal.EffectiveUntil);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_NoEdits_Future_Activation()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var effectiveDate1 = DateTimeOffset.Now.AddSeconds(2);
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);
                    session.SaveChanges();
                }

                // Check the results - there shouldn't be a current doc yet.
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Null(current);
                }

                // wait for activation - allow a little extra time for the activator to complete
                Thread.Sleep(2500);

                // Check the results again - now we should have the current doc.
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.NotNull(current);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_OneEdit()
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

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(20, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(2, currentTemporal.RevisionNumber);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(2, revisions.Length);

                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, revisions[0].Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 2, revisions[1].Id);
                    Assert.Equal(10, revisions[0].PayRate);
                    Assert.Equal(20, revisions[1].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate2, version1Temporal.EffectiveUntil);

                    var version2Temporal = session.Advanced.GetTemporalMetadataFor(revisions[1]);
                    Assert.Equal(TemporalStatus.Revision, version2Temporal.Status);
                    Assert.False(version2Temporal.Deleted);
                    Assert.Equal(effectiveDate2, version2Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version2Temporal.EffectiveUntil);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_OneEdit_Activation()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                const string id = "employees/1";
                var effectiveDate1 = DateTimeOffset.Now;
                using (var session = documentStore.OpenSession())
                {
                    var employee = new Employee { Id = id, Name = "John", PayRate = 10 };
                    session.Effective(effectiveDate1).Store(employee);
                    session.SaveChanges();
                }

                // Make some changes
                var effectiveDate2 = effectiveDate1.AddSeconds(2);
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Load<Employee>(id);
                    session.PrepareNewRevision(employee, effectiveDate2);
                    employee.PayRate = 20;

                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(10, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(1, currentTemporal.RevisionNumber);
                }

                // wait for activation - allow a little extra time for the activator to complete
                Thread.Sleep(2500);

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(id, current.Id);
                    Assert.Equal(20, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(2, currentTemporal.RevisionNumber);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_TwoEdits()
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

                // Make some more changes
                var effectiveDate3 = new DateTimeOffset(new DateTime(2012, 3, 1));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Load<Employee>(id);
                    session.PrepareNewRevision(employee, effectiveDate3);
                    employee.PayRate = 30;

                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(30, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(3, currentTemporal.RevisionNumber);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(3, revisions.Length);

                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, revisions[0].Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 2, revisions[1].Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 3, revisions[2].Id);
                    Assert.Equal(10, revisions[0].PayRate);
                    Assert.Equal(20, revisions[1].PayRate);
                    Assert.Equal(30, revisions[2].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate2, version1Temporal.EffectiveUntil);

                    var version2Temporal = session.Advanced.GetTemporalMetadataFor(revisions[1]);
                    Assert.Equal(TemporalStatus.Revision, version2Temporal.Status);
                    Assert.False(version2Temporal.Deleted);
                    Assert.Equal(effectiveDate2, version2Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate3, version2Temporal.EffectiveUntil);

                    var version3Temporal = session.Advanced.GetTemporalMetadataFor(revisions[2]);
                    Assert.Equal(TemporalStatus.Revision, version3Temporal.Status);
                    Assert.False(version3Temporal.Deleted);
                    Assert.Equal(effectiveDate3, version3Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version3Temporal.EffectiveUntil);
                }
            }
        }

        [Fact]
        public void TemporalVersioning_TwoEdits_SecondOverridingFirst()
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

                // Make some more changes - at an earlier date than the previous edit
                var effectiveDate3 = new DateTimeOffset(new DateTime(2012, 1, 15));
                using (var session = documentStore.OpenSession())
                {
                    var employee = session.Load<Employee>(id);
                    session.PrepareNewRevision(employee, effectiveDate3);
                    employee.PayRate = 30;

                    session.SaveChanges();
                }

                // Check the results
                using (var session = documentStore.OpenSession())
                {
                    var current = session.Load<Employee>(id);
                    Assert.Equal(30, current.PayRate);

                    var currentTemporal = session.Advanced.GetTemporalMetadataFor(current);
                    Assert.Equal(TemporalStatus.Current, currentTemporal.Status);
                    Assert.Equal(3, currentTemporal.RevisionNumber);

                    var revisions = session.Advanced.GetTemporalRevisionsFor<Employee>(id, 0, 10);
                    Assert.Equal(3, revisions.Length);

                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 1, revisions[0].Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 2, revisions[1].Id);
                    Assert.Equal(id + TemporalConstants.TemporalKeySeparator + 3, revisions[2].Id);
                    Assert.Equal(10, revisions[0].PayRate);
                    Assert.Equal(20, revisions[1].PayRate);
                    Assert.Equal(30, revisions[2].PayRate);

                    var version1Temporal = session.Advanced.GetTemporalMetadataFor(revisions[0]);
                    Assert.Equal(TemporalStatus.Revision, version1Temporal.Status);
                    Assert.False(version1Temporal.Deleted);
                    Assert.Equal(effectiveDate1, version1Temporal.EffectiveStart);
                    Assert.Equal(effectiveDate3, version1Temporal.EffectiveUntil);

                    // the middle one now is an artifact
                    var version2Temporal = session.Advanced.GetTemporalMetadataFor(revisions[1]);
                    Assert.Equal(TemporalStatus.Artifact, version2Temporal.Status);
                    Assert.False(version2Temporal.Deleted);
                    Assert.Equal(effectiveDate2, version2Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version2Temporal.EffectiveUntil);

                    var version3Temporal = session.Advanced.GetTemporalMetadataFor(revisions[2]);
                    Assert.Equal(TemporalStatus.Revision, version3Temporal.Status);
                    Assert.False(version3Temporal.Deleted);
                    Assert.Equal(effectiveDate3, version3Temporal.EffectiveStart);
                    Assert.Equal(DateTimeOffset.MaxValue, version3Temporal.EffectiveUntil);

                    //TODO: Check temporal index to ensure artifact isn't considered
                }
            }
        }
    }
}
