using System;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;
using Raven.Database.Server;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning
{
    public class HttpTests : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_OneEdit_OverHttp()
        {
            using (var embeddedDocumentStore = this.GetTemporalDocumentStore())
            {
                embeddedDocumentStore.Configuration.AnonymousUserAccessMode = AnonymousUserAccessMode.All;
                using (var server = new HttpServer(embeddedDocumentStore.Configuration, embeddedDocumentStore.DocumentDatabase))
                {
                    server.StartListening();

                    var documentStore = new DocumentStore { Url = "http://localhost:8080" };
                    documentStore.Initialize();
                    documentStore.InitializeTemporalVersioning();

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
        }
    }
}
