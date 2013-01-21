using System;
using System.Collections.Generic;

#if CLIENT
namespace Raven.Client.Bundles.TemporalVersioning.Common
#else

namespace Raven.Bundles.TemporalVersioning.Common
#endif
{
    public class TemporalHistory
    {
        public const string KeyExt = "/temporalhistory";

        public static string GetKeyFor(string id)
        {
            return id + KeyExt;
        }

        internal TemporalHistory()
        {
            Revisions = new List<RevisionInfo>();
        }

        public IList<RevisionInfo> Revisions { get; set; }

        public class RevisionInfo
        {
            public string Key { get; set; }
            public TemporalStatus Status { get; set; }
            public bool Deleted { get; set; }
            public bool Pending { get; set; }
            public DateTimeOffset EffectiveStart { get; set; }
            public DateTimeOffset EffectiveUntil { get; set; }
            public DateTimeOffset AssertedStart { get; set; }
            public DateTimeOffset AssertedUntil { get; set; }
        }

        public void AddRevision(string key, TemporalMetadata temporal)
        {
            if (temporal.EffectiveStart == null || temporal.EffectiveUntil == null ||
                temporal.AssertedStart == null || temporal.AssertedUntil == null)
                throw new ArgumentException("All temporal dates must be entered.");

            Revisions.Add(new RevisionInfo {
                                               Key = key,
                                               Status = temporal.Status,
                                               Deleted = temporal.Deleted,
                                               Pending = temporal.Pending,
                                               EffectiveStart = temporal.EffectiveStart.Value,
                                               EffectiveUntil = temporal.EffectiveUntil.Value,
                                               AssertedStart = temporal.AssertedStart.Value,
                                               AssertedUntil = temporal.AssertedUntil.Value
                                           });
        }
    }
}
