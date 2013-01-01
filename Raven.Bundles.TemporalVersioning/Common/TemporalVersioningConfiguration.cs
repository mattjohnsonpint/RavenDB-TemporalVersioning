#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else
namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public class TemporalVersioningConfiguration
    {
        /// <summary>
        /// Id can be in the following format:
        /// 1. Raven/TemporalVersioning/{Raven-Entity-Name} - When using this format, the impacted documents are just documents with the corresponding Raven-Entity-Name metadata.
        /// 2. Raven/TemporalVersioning/DefaultConfiguration - This is a global configuration, which impacts just documents that don't have a specific Raven/Versioning/{Raven-Entity-Name} corresponding to them.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Enable or disable temporal versioning.
        /// </summary>
        public bool Enabled { get; set; }

    }
}