using System;

namespace Raven.Bundles.Tests.TemporalVersioning.Entities
{
    // PayCheck is an example of a non-temporal entity that has its own temporal context.
    // The context is the date that the check was issued.

    public class PayCheck
    {
        public string Id { get; set; }
        public DateTime Issued { get; set; }
        public string EmployeeId { get; set; }
        public decimal Amount { get; set; }
    }
}
