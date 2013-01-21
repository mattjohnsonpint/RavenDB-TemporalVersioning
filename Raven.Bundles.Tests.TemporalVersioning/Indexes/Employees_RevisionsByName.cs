using System;
using System.Linq;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    public class Employees_RevisionsByName : AbstractIndexCreationTask<Employee>
    {
        // Include the name for indexing, and the effective date for sorting

        public Employees_RevisionsByName()
        {
            Map = employees => from employee in employees
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalMetadata.RavenDocumentTemporalStatus)
                               let effective = MetadataFor(employee).Value<DateTime>(TemporalMetadata.RavenDocumentTemporalEffectiveStart)
                               where status == TemporalStatus.Revision
                               select new
                                      {
                                          employee.Name,
                                          Effective = effective
                                      };
        }
    }
}
