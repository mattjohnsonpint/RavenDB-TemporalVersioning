using System.Linq;
using Raven.Abstractions.Indexing;
using Raven.Bundles.Tests.TemporalVersioning.Entities;
using Raven.Bundles.Tests.TemporalVersioning.Projections;
using Raven.Client.Bundles.TemporalVersioning.Common;
using Raven.Client.Indexes;

namespace Raven.Bundles.Tests.TemporalVersioning.Indexes
{
    /// <summary>
    /// Index with a NT:Tx relationship.
    /// A PayCheck is indexed by the employee's name and pay rate at the time the check was issued.
    /// </summary>
    /// <remarks>
    /// The join uses the history document to pull the correct revision of the employee.
    /// </remarks>
    public class PayChecks_ByEmployee : AbstractIndexCreationTask<PayCheck, PayCheckWithEmployeeInfo>
    {
        public PayChecks_ByEmployee()
        {
            Map = payChecks => from payCheck in payChecks
                               let employeeHistory = LoadDocument<TemporalHistory>(payCheck.EmployeeId + TemporalHistory.KeyExt)
                               from employeeRevision in employeeHistory.Revisions
                               where employeeRevision.Status == TemporalStatus.Revision &&
                                     employeeRevision.EffectiveStart <= payCheck.Issued &&
                                     employeeRevision.EffectiveUntil > payCheck.Issued
                               let employee = LoadDocument<Employee>(employeeRevision.Key)
                               select new
                                      {
                                          payCheck.Issued,
                                          EmployeeName = employee.Name,
                                          employee.PayRate
                                      };

            // These fields are stored so we can return them in a projection.
            Store(x => x.EmployeeName, FieldStorage.Yes);
            Store(x => x.PayRate, FieldStorage.Yes);
        }
    }
}
