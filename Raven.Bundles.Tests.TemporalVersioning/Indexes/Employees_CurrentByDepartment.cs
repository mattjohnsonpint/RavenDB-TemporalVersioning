using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tc:NT relationship.
    /// Current employees are indexed by their name and department name.
    /// </summary>
    public class Employees_CurrentByDepartment : AbstractIndexCreationTask<Employee, EmployeeWithDepartment>
    {
        public Employees_CurrentByDepartment()
        {
            Map = employees => from employee in employees
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalMetadata.RavenDocumentTemporalStatus)
                               where status == TemporalStatus.Current
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
