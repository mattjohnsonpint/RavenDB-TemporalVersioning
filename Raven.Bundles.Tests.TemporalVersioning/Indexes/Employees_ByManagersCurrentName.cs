using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tr:Tc relationship.
    /// All revisions of employees are indexed by their name and their manager's current name.
    /// </summary>
    /// <remarks>
    /// This is a bit contrived, as we'd probably not care about the manager's current name,
    /// but their name at the time they were managing the employee, which is a Tr:Tx or Tr:Tr relationship.
    /// </remarks>
    public class Employees_ByManagersCurrentName : AbstractIndexCreationTask<Employee, EmployeeWithManager>
    {
        public Employees_ByManagersCurrentName()
        {
            Map = employees => from employee in employees
                               let manager = LoadDocument<Employee>(employee.ManagerId)
                               select new
                                      {
                                          employee.Name,
                                          Manager = manager.Name
                                      };

            // The manager field is stored so we can return it in a projection.
            Store(x => x.Manager, FieldStorage.Yes);
        }
    }
}
