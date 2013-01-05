using System;
using System.Collections.Generic;
using System.Linq;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database;

namespace Raven.Bundles.TemporalVersioning
{
    internal class PendingRevisionsIndex
    {
        private const string Activation = "Activation";

        public static void CreateIndex(DocumentDatabase database)
        {
            var index = new IndexDefinition {
                                                Map = string.Format(
                                                    @"from doc in docs
where doc[""{0}""][""{1}""] == ""{2}""
   && doc[""{0}""][""{3}""] == true
select new
{{
    {4} = doc[""{0}""][""{5}""],
}}",
                                                    Constants.Metadata,
                                                    TemporalMetadata.RavenDocumentTemporalStatus, TemporalStatus.Revision,
                                                    TemporalMetadata.RavenDocumentTemporalPending,
                                                    Activation, TemporalMetadata.RavenDocumentTemporalEffectiveStart)
                                            };

            if (database.GetIndexDefinition(TemporalConstants.PendingRevisionsIndex) == null)
                database.PutIndex(TemporalConstants.PendingRevisionsIndex, index);
        }

        public static DateTime GetNextActivationDate(DocumentDatabase database)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.PendingRevisionsIndex, currentTime, null);

            var query = new IndexQuery {
                                           Start = 0,
                                           PageSize = 1,
                                           Cutoff = currentTime,
                                           FieldsToFetch = new[] { Activation },
                                           SortedFields = new[] { new SortedField(Activation) }
                                       };

            var result = database.Query(TemporalConstants.PendingRevisionsIndex, query).Results.FirstOrDefault();
            return result == null ? DateTime.MaxValue : result.Value<DateTime>(Activation);
        }

        public static IList<string> GetRevisionsRequiringActivation(DocumentDatabase database)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.PendingRevisionsIndex, currentTime, null);

            const int pageSize = 1024;

            var qs = string.Format("{0}:[* TO {1:o}]", Activation, currentTime);

            var query = new IndexQuery {
                                           Start = 0,
                                           PageSize = pageSize,
                                           Cutoff = currentTime,
                                           Query = qs,
                                           FieldsToFetch = new[] { Constants.DocumentIdFieldName },
                                           SortedFields = new[] { new SortedField(Activation) }
                                       };

            var list = new List<string>();
            while (true)
            {
                var results = database.Query(TemporalConstants.PendingRevisionsIndex, query).Results;
                list.AddRange(results.Select(x => x.Value<string>(Constants.DocumentIdFieldName)));
                if (results.Count < pageSize)
                    return list;
                query.Start += pageSize;
            }
        }
    }
}
