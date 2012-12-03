using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning.Common
{
    public static class TemporalExtensions
    {
        public static TemporalMetadata GetTemporalMetadata(this RavenJObject metadata)
        {
            return new TemporalMetadata(metadata);
        }
    }
}
