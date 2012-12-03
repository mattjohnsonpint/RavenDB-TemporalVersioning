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
                               let status = MetadataFor(employee)[TemporalConstants.RavenDocumentTemporalStatus]
                               where status.ToString() == TemporalStatus.Current.ToString()
                               select new
                                   {
                                       employee.Name
                                   };
        }
    }
}
