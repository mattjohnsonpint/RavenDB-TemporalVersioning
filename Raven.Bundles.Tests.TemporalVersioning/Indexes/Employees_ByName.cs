using System.Linq;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    public class Employees_ByName : AbstractIndexCreationTask<Employee>
    {
        public Employees_ByName()
        {
            Map = employees => from employee in employees
                               select new {
                                              employee.Name
                                          };
        }
    }
}
