using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a NT:NT relationship.
    /// A department is indexed by its name and division name.
    /// </summary>
    public class Departments_ByDivision : AbstractIndexCreationTask<Department, DepartmentWithDivision>
    {
        public Departments_ByDivision()
        {
            Map = departments => from department in departments
                                 let division = LoadDocument<Division>(department.DivisionId)
                                 select new
                                        {
                                            department.Name,
                                            Division = division.Name
                                        };

            // The division field is stored so we can return it in a projection.
            Store(x => x.Division, FieldStorage.Yes);
        }
    }
}
