using Raven.Json.Linq;

#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else
namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public static class TemporalExtensions
    {
        public static TemporalMetadata GetTemporalMetadata(this RavenJObject metadata)
        {
            return new TemporalMetadata(metadata);
        }
    }
}
