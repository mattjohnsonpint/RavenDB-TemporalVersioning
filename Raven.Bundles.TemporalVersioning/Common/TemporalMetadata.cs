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
	    public const string TemporalEffectiveDate = "Temporal-Effective-Date";
	    public const string RavenDocumentTemporalRevision = "Raven-Document-Temporal-Revision";
	    public const string RavenDocumentTemporalStatus = "Raven-Document-Temporal-Status";
	    public const string RavenDocumentTemporalEffectiveStart = "Raven-Document-Temporal-Effective-Start";
	    public const string RavenDocumentTemporalEffectiveUntil = "Raven-Document-Temporal-Effective-Until";
	    public const string RavenDocumentTemporalDeleted = "Raven-Document-Temporal-Deleted";
	    public const string RavenDocumentTemporalPending = "Raven-Document-Temporal-Pending";

	    private readonly RavenJObject _metadata;

        public TemporalMetadata(RavenJObject metadata)
        {
            _metadata = metadata;
        }

        public int RevisionNumber
        {
            get
            {
                var revision = _metadata.Value<int?>(RavenDocumentTemporalRevision);
                return revision.HasValue ? revision.Value : 0;
            }
            set
            {
                const string key = RavenDocumentTemporalRevision;

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
                return Enum.TryParse(_metadata.Value<string>(RavenDocumentTemporalStatus), out status)
                           ? status
                           : TemporalStatus.NonTemporal;
            }
            set
            {
                const string key = RavenDocumentTemporalStatus;

                if (value != TemporalStatus.NonTemporal)
                    _metadata[key] = value.ToString();
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public bool Deleted
        {
            get { return _metadata.Value<bool?>(RavenDocumentTemporalDeleted) ?? false; }
            set { _metadata[RavenDocumentTemporalDeleted] = value; }
        }

        public bool Pending
        {
            get { return _metadata.Value<bool?>(RavenDocumentTemporalPending) ?? false; }
            set { _metadata[RavenDocumentTemporalPending] = value; }
        }

        public DateTimeOffset? Effective
        {
            get { return _metadata.Value<DateTimeOffset?>(TemporalEffectiveDate); }
            set
            {
                const string key = TemporalEffectiveDate;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public DateTimeOffset? EffectiveStart
        {
            get { return _metadata.Value<DateTimeOffset?>(RavenDocumentTemporalEffectiveStart); }
            set
            {
                const string key = RavenDocumentTemporalEffectiveStart;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }

        public DateTimeOffset? EffectiveUntil
        {
            get { return _metadata.Value<DateTimeOffset?>(RavenDocumentTemporalEffectiveUntil); }
            set
            {
                const string key = RavenDocumentTemporalEffectiveUntil;

                if (value.HasValue)
                    _metadata[key] = value;
                else if (_metadata.ContainsKey(key))
                    _metadata.Remove(key);
            }
        }
    }
}
