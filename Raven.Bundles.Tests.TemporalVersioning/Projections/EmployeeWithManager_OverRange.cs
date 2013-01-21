using System;

namespace Raven.Bundles.Tests.TemporalVersioning.Projections
{
    public class EmployeeWithManager_OverRange
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Manager { get; set; }
        public DateTimeOffset ManagerEffectiveStart { get; set; }
        public DateTimeOffset ManagerEffectiveUntil { get; set; }
    }
}