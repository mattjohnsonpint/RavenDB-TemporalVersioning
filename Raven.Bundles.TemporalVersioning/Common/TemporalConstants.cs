#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else
namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public static class TemporalConstants
    {
        public const string BundleName = "TemporalVersioning";
        public const string TemporalKeySeparator = "/temporalrevisions/";
        public const string PendingRevisionsIndex = "Raven/TemporalRevisions/Pending";
    }
}
