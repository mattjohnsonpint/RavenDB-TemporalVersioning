namespace Raven.Bundles.TemporalVersioning.Common
{
    public class TemporalVersioningConfiguration
    {
        /// <summary>
        /// Id can be in the following format:
        /// 1. Raven/TemporalVersioning/{Raven-Entity-Name} - When using this format, the impacted documents are just documents with the corresponing Raven-Entity-Name metadata.
        /// 2. Raven/TemporalVersioning/DefaultConfiguration - This is a global configuration, which impacts just documents that don't have a specifc Raven/Versioning/{Raven-Entity-Name} corresponed to them.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Enable or disable temporal versioning.
        /// </summary>
        public bool Enabled { get; set; }

    }
}