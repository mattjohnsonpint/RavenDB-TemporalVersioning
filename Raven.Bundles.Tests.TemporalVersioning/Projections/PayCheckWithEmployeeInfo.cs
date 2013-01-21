using System;

namespace Raven.Bundles.Tests.TemporalVersioning.Projections
{
    public class PayCheckWithEmployeeInfo
    {
        public string Id { get; set; }
        public DateTime Issued { get; set; }
        public string Amount { get; set; }
        public string EmployeeName { get; set; }
        public decimal PayRate { get; set; }
    }
}
