#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else
namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public enum TemporalStatus
    {
        NonTemporal,
        Current,
        Revision,
        Artifact
    }
}