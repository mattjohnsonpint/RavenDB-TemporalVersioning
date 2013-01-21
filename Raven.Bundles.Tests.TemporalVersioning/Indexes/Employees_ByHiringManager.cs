using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a Tr:Tx relationship.
    /// All revisions of employees are indexed by their name and their hiring manager's name on the day they were hired.
    /// Complexity arises when its possible that employee had no hiring manager.
    /// </summary>
    public class Employees_ByHiringManager : AbstractIndexCreationTask<Employee, EmployeeWithManager>
    {
        public Employees_ByHiringManager()
        {
            Map = employees => from employee in employees

                               // filter the index to just revisions
                               let status = MetadataFor(employee).Value<TemporalStatus>(TemporalMetadata.RavenDocumentTemporalStatus)
                               where status == TemporalStatus.Revision

                               // employee.Id will refer to the full revision key "employees/1/temporalrevisions/2"
                               // so we need to truncate it to get back the true employeeId
                               let employeeId = employee.Id.Substring(0, employee.Id.Length - TemporalConstants.TemporalKeySeparator.Length - 1)
                               
                               // Find the revision of the employee that was effective on the hire date.
                               // Note that we have to use the history document for this.
                               // Revision 1 isn't necessarily the correct revision, because it may have been artifacted.
                               let employeeRevAtHiring = LoadDocument<Employee>(
                                   LoadDocument<TemporalHistory>(employeeId + TemporalHistory.KeyExt)
                                       .Revisions.First(x => x.Status == TemporalStatus.Revision &&
                                                             x.EffectiveStart <= employee.HireDate &&
                                                             x.EffectiveUntil > employee.HireDate).Key)

                               // If the employee has a manager, find the revision of that manager that was effective on the hire date.
                               // The null check is important, because it is possible there was no manager on the hire date.
                               let manager = employeeRevAtHiring.ManagerId == null
                                                 ? null
                                                 : LoadDocument<Employee>(
                                                     LoadDocument<TemporalHistory>(employeeRevAtHiring.ManagerId + TemporalHistory.KeyExt)
                                                         .Revisions.First(x => x.Status == TemporalStatus.Revision &&
                                                                               x.EffectiveStart <= employee.HireDate &&
                                                                               x.EffectiveUntil > employee.HireDate).Key)

                               // We should now have the items to index.  Don't forget to handle the null manager.
                               select new
                                      {
                                          employee.Name,
                                          Manager = manager == null ? null : manager.Name
                                      };

            // The manager field is stored so we can return it in a projection.
            Store(x => x.Manager, FieldStorage.Yes);
        }
    }
}
