using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a NT:Tc relationship.
    /// A department is indexed by it's name and the supervisor's current name.
    /// </summary>
    public class Departments_BySupervisor : AbstractIndexCreationTask<Department, DepartmentWithSupervisor>
    {
        public Departments_BySupervisor()
        {
            Map = departments => from department in departments
                                 let supervisor = LoadDocument<Employee>(department.SupervisorEmployeeId)
                                 select new
                                        {
                                            department.Name,
                                            Supervisor = supervisor.Name
                                        };

            // The supervisor field is stored so we can return it in a projection.
            Store(x => x.Supervisor, FieldStorage.Yes);
        }
    }
}
