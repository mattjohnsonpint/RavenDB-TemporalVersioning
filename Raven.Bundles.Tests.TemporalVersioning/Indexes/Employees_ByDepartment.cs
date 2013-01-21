using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tr:NT relationship.
    /// All revisions of employees are indexed by their name and department name.
    /// </summary>
    public class Employees_ByDepartment : AbstractIndexCreationTask<Employee, EmployeeWithDepartment>
    {
        public Employees_ByDepartment()
        {
            Map = employees => from employee in employees
                               let department = LoadDocument<Department>(employee.DepartmentId)
                               select new
                                      {
                                          employee.Name,
                                          Department = department.Name
                                      };

            // The department field is stored so we can return it in a projection.
            Store(x => x.Department, FieldStorage.Yes);
        }
    }
}
