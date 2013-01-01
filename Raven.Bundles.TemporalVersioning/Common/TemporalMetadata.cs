using System;
using Raven.Json.Linq;

#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else
namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public class TemporalMetadata
    {
        private readonly RavenJObject _metadata;

        public TemporalMetadata(RavenJObject metadata)
        {
            _metadata = metadata;
        }

        public int RevisionNumber
        {
            get
            {
                var revision = _metadata.Value<int?>(TemporalConstants.RavenDocumentTemporalRevision);
                return revision.HasValue ? revision.Value : 0;
            }
            set
            {
                const string key = TemporalConstants.RavenDocumentTemporalRevision;

                if (value > 0)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public TemporalStatus Status
        {
            get
            {
                TemporalStatus status;
                return Enum.TryParse(_metadata.Value<string>(TemporalConstants.RavenDocumentTemporalStatus), out status)
                           ? status
                           : TemporalStatus.NonTemporal;
            }
            set
            {
                const string key = TemporalConstants.RavenDocumentTemporalStatus;

                if (value != TemporalStatus.NonTemporal)
                    _metadata[key] = value.ToString();
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public bool Deleted
        {
            get { return _metadata.Value<bool?>(TemporalConstants.RavenDocumentTemporalDeleted) ?? false; }
            set { _metadata[TemporalConstants.RavenDocumentTemporalDeleted] = value; }
        }

        public bool Pending
        {
            get { return _metadata.Value<bool?>(TemporalConstants.RavenDocumentTemporalPending) ?? false; }
            set { _metadata[TemporalConstants.RavenDocumentTemporalPending] = value; }
        }

        public DateTime? Effective
        {
            get { return _metadata.Value<DateTime?>(TemporalConstants.TemporalEffectiveDate); }
            set
            {
                const string key = TemporalConstants.TemporalEffectiveDate;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public DateTimeOffset? EffectiveStart
        {
            get { return _metadata.Value<DateTimeOffset?>(TemporalConstants.RavenDocumentTemporalEffectiveStart); }
            set
            {
                const string key = TemporalConstants.RavenDocumentTemporalEffectiveStart;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public DateTimeOffset? EffectiveUntil
        {
            get { return _metadata.Value<DateTimeOffset?>(TemporalConstants.RavenDocumentTemporalEffectiveUntil); }
            set
            {
                const string key = TemporalConstants.RavenDocumentTemporalEffectiveUntil;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }
    }
}
