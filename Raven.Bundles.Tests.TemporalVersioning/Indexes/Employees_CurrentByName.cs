using System.Linq;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    public class Employees_CurrentByName : AbstractIndexCreationTask<Employee>
    {
        public Employees_CurrentByName()
        {
            Map = employees => from employee in employees
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalConstants.RavenDocumentTemporalStatus)
                               where status == TemporalStatus.Current
                               select new {
                                              employee.Name
                                          };
        }
    }
}
