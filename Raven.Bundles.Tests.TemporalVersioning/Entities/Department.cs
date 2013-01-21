namespace Raven.Bundles.Tests.TemporalVersioning.Entities
{
    // Example of a non-temporal entity.
    // It *could* be tracked temporally if we cared about changes in the department name or supervisor.
    // For purposes of demonstration, we won't track those changes.

    public class Department
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string DivisionId { get; set; }
        public string SupervisorEmployeeId { get; set; }
    }
}
