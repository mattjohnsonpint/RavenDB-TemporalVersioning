using System.Linq;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Indexes;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client;
using Raven.Client.Linq;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Bundles.Tests.TemporalVersioning.RelationshipTests
{
    public class Relationship_Nt_Nt : RavenTestBase
    {
        [Fact]
        public void TemporalVersioning_Relationship_Nt_Nt()
        {
            using (var documentStore = this.GetTemporalDocumentStore())
            {
                documentStore.ExecuteIndex(new Departments_ByDivision());

                using (var session = documentStore.OpenSession())
                {
                    session.Store(new Division { Id = "divisions/1", Name = "Eastern" });
                    session.Store(new Division { Id = "divisions/2", Name = "Western" });

                    session.Store(new Department { Id = "departments/1", DivisionId = "divisions/1", Name = "Accounting" });
                    session.Store(new Department { Id = "departments/2", DivisionId = "divisions/1", Name = "Sales" });
                    session.Store(new Department { Id = "departments/3", DivisionId = "divisions/1", Name = "Support" });

                    session.Store(new Department { Id = "departments/4", DivisionId = "divisions/2", Name = "Accounting" });
                    session.Store(new Department { Id = "departments/5", DivisionId = "divisions/2", Name = "Sales" });
                    session.Store(new Department { Id = "departments/6", DivisionId = "divisions/2", Name = "Support" });

                    session.SaveChanges();
                }

                using (var session = documentStore.OpenSession())
                {
                    var results = session.Query<DepartmentWithDivision, Departments_ByDivision>()
                                         .Customize(x => x.WaitForNonStaleResults())
                                         .Where(x => x.Division == "Western")
                                         .OrderBy(x => x.Name)
                                         .AsProjection<DepartmentWithDivision>()
                                         .ToList();

                    Assert.Equal(3, results.Count);

                    Assert.Equal("Accounting", results[0].Name);
                    Assert.Equal("Sales", results[1].Name);
                    Assert.Equal("Support", results[2].Name);

                    Assert.Equal("Western", results[0].Division);
                    Assert.Equal("Western", results[1].Division);
                    Assert.Equal("Western", results[2].Division);
                }
            }
        }
    }
}
