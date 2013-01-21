using System;
using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tr:Tr relationship.
    /// All revisions of employees are indexed by their name and their manager's name.
    /// </summary>
    /// <remarks>
    /// Even if the employee was always managed by the same manager, we can't assume the manager
    /// information was the same for the entire revision of the employee.
    /// This is what makes a Tr:Tr relationship complicated.
    /// 
    /// For example, the employee may have changed names, and the manager may have changed names also.
    /// Assuming this happened at two different times, there are three periods of data differences:
    ///   Emp: OLD  NEW  NEW
    ///   Mgr: OLD  OLD  NEW
    /// For emp revision 1, we have only one match, but for emp revsion 2, we have TWO matches.
    /// So we need to know the split points of the corresponding data and adjust accordingly.
    /// </remarks>
    public class Employees_ByManager : AbstractIndexCreationTask<Employee, EmployeeWithManager>
    {
        public Employees_ByManager()
        {
            Map = employees => from employee in employees

                               // filter the index to just revisions
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalMetadata.RavenDocumentTemporalStatus)
                               where status == TemporalStatus.Revision

                               // employee.Id will refer to the full revision key "employees/1/temporalrevisions/2"
                               // so we need to truncate it to get back the true employeeId
                               let employeeId = employee.Id.Substring(0, employee.Id.Length - TemporalConstants.TemporalKeySeparator.Length - 1)

                               // get the range for this employee revision
                               let empStart = MetadataFor(employee).Value<DateTimeOffset>(TemporalMetadata.RavenDocumentTemporalEffectiveStart)
                               let empUntil = MetadataFor(employee).Value<DateTimeOffset>(TemporalMetadata.RavenDocumentTemporalEffectiveUntil)

                               // If the employee has a manager, find the revisions of that manager that were effective over this range.
                               // The null check is important, because it is possible there is no manager.
                               // Watch the comparison, it's different here.  This is a range intersection.
                               let managerRevs = employee.ManagerId == null
                                                     ? (Employee[]) new object[] { null }
                                                     : LoadDocument<TemporalHistory>(employee.ManagerId + TemporalHistory.KeyExt)
                                                           .Revisions.Where(x => x.Status == TemporalStatus.Revision &&
                                                                                 x.EffectiveStart < empUntil &&
                                                                                 x.EffectiveUntil > empStart)
                                                           .Select(x => LoadDocument<Employee>(x.Key))

                               // Map a separate item for each manager revision.  We'll need the Start/Until dates for the manager also.
                               from manager in managerRevs
                               let mgrStart =
                                   manager == null
                                       ? DateTimeOffset.MinValue
                                       : MetadataFor(manager).Value<DateTimeOffset>(TemporalMetadata.RavenDocumentTemporalEffectiveStart)
                               let mgrUntil =
                                   manager == null
                                       ? DateTimeOffset.MaxValue
                                       : MetadataFor(manager).Value<DateTimeOffset>(TemporalMetadata.RavenDocumentTemporalEffectiveUntil)

                               // We should now have the items to index.  Don't forget to handle the null manager.
                               select new
                                      {
                                          employee.Name,
                                          Manager = manager == null ? null : manager.Name,

                                          // This is the ugly part - we must put these in the index so we can
                                          // manually filter in a where clause.  There is no automatic way
                                          // that the bundle can do this for you.
                                          ManagerEffectiveStart = new[] { empStart, mgrStart }.Max(),
                                          ManagerEffectiveUntil = new[] { empUntil, mgrUntil }.Min(),
                                      };

            // The manager field is stored so we can return it in a projection.
            Store(x => x.Manager, FieldStorage.Yes);
        }
    }
}
