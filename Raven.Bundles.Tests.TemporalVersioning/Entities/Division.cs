namespace Raven.Bundles.Tests.TemporalVersioning.Entities
{
    // Example of a non-temporal entity.
    // It *could* be tracked temporally if we cared about changes in the division name.
    // For purposes of demonstration, we won't track those changes.

    public class Division
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
