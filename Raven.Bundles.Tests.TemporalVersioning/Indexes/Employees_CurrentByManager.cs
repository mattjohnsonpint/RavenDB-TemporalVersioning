using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tc:Tc relationship.
    /// Current employees are indexed by their name and their current manager's current name.
    /// </summary>
    public class Employees_CurrentByManager : AbstractIndexCreationTask<Employee, EmployeeWithManager>
    {
        public Employees_CurrentByManager()
        {
            Map = employees => from employee in employees
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalMetadata.RavenDocumentTemporalStatus)
                               where status == TemporalStatus.Current
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
