using System.Linq;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    public class Employees_CurrentCount : AbstractIndexCreationTask<Employee, Employees_CurrentCount.Result>
    {
        public class Result
        {
            public int Count { get; set; }
        }

        public Employees_CurrentCount()
        {
            Map = employees => from employee in employees
                               let status = MetadataFor(employee)[TemporalConstants.RavenDocumentTemporalStatus]
                               where status.ToString() == TemporalStatus.Current.ToString()
                               select new {
                                              Count = 1
                                          };

            Reduce = results => from result in results
                                group result by 0
                                into g
                                select new {
                                               Count = g.Sum(x => x.Count)
                                           };
        }
    }
}
