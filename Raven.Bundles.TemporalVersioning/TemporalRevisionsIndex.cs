using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Raven.Abstractions;
using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database;

namespace Raven.Bundles.TemporalVersioning
{
    internal class TemporalRevisionsIndex
    {
        private const string EffectiveStart = "EffectiveStart";
        private const string EffectiveUntil = "EffectiveUntil";
        private const string Deleted = "Deleted";
        private const string Pending = "Pending";

        public static void CreateIndex(DocumentDatabase database)
        {
            var index = new IndexDefinition {
                                                Map = string.Format(
                                                    @"from doc in docs
where doc[""{0}""][""{1}""] == ""{2}""
select new
{{
    {3} = doc[""{0}""][""{4}""],
    {5} = doc[""{0}""][""{6}""],
    {7} = doc[""{0}""][""{8}""],
    {9} = doc[""{0}""][""{10}""]
}}",
                                                    Constants.Metadata,
                                                    TemporalConstants.RavenDocumentTemporalStatus, TemporalStatus.Revision,
                                                    EffectiveStart, TemporalConstants.RavenDocumentTemporalEffectiveStart,
                                                    EffectiveUntil, TemporalConstants.RavenDocumentTemporalEffectiveUntil,
                                                    Deleted, TemporalConstants.RavenDocumentTemporalDeleted,
                                                    Pending, TemporalConstants.RavenDocumentTemporalPending)
                                            };

            if (database.GetIndexDefinition(TemporalConstants.TemporalRevisionsIndex) == null)
                database.PutIndex(TemporalConstants.TemporalRevisionsIndex, index);
        }

        public static IList<string> GetFutureRevisions(DocumentDatabase database, string key, DateTimeOffset effective)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.TemporalRevisionsIndex, currentTime, null);

            const int pageSize = 1024;

            var qs = string.Format("{0}:{1}* AND {2}:[{3:o} TO NULL]",
                                   Constants.DocumentIdFieldName, key,
                                   EffectiveStart, effective.UtcDateTime);

            var query = new IndexQuery {
                                           Start = 0,
                                           PageSize = pageSize,
                                           Cutoff = currentTime,
                                           Query = qs,
                                           FieldsToFetch = new[] { Constants.DocumentIdFieldName },
                                           SortedFields = new[] { new SortedField(EffectiveStart) }
                                       };

            var list = new List<string>();
            while (true)
            {
                var results = database.Query(TemporalConstants.TemporalRevisionsIndex, query).Results;
                list.AddRange(results.Select(x => x.Value<string>(Constants.DocumentIdFieldName)));
                if (results.Count < pageSize)
                    return list;
                query.Start += pageSize;
            }
        }

        public static string GetLastRevision(DocumentDatabase database, string key, DateTimeOffset effective)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.TemporalRevisionsIndex, currentTime, null);

            var qs = string.Format("{0}:{1}* AND {2}:{{* TO {3}}}",
                                   Constants.DocumentIdFieldName, key,
                                   EffectiveStart, effective.UtcDateTime.ToString("o"));

            var query = new IndexQuery {
                                           Start = 0,
                                           PageSize = 1,
                                           Cutoff = currentTime,
                                           Query = qs,
                                           FieldsToFetch = new[] { Constants.DocumentIdFieldName },
                                           SortedFields = new[] { new SortedField(EffectiveStart) { Descending = true } }
                                       };

            var result = database.Query(TemporalConstants.TemporalRevisionsIndex, query).Results.FirstOrDefault();
            return result == null ? null : result.Value<string>(Constants.DocumentIdFieldName);
        }

        public static string GetActiveRevision(DocumentDatabase database, string key, DateTimeOffset effectiveDate)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.TemporalRevisionsIndex, currentTime, null);

            var qs = string.Format("{0}:{1}* AND {2}:[* TO {4:o}] AND {3}:{{{4:o} TO NULL}} AND {5}:{6}",
                                   Constants.DocumentIdFieldName, key,
                                   EffectiveStart, EffectiveUntil, effectiveDate.UtcDateTime,
                                   Deleted, false);

            var query = new IndexQuery {
                                           Start = 0,
                                           PageSize = 1,
                                           Cutoff = currentTime,
                                           Query = qs,
                                           FieldsToFetch = new[] { Constants.DocumentIdFieldName },
                                           SortedFields = new[] { new SortedField(EffectiveStart) { Descending = true } }
                                       };

            var result = database.Query(TemporalConstants.TemporalRevisionsIndex, query).Results.FirstOrDefault();
            return result == null ? null : result.Value<string>(Constants.DocumentIdFieldName);
        }

        public static DateTime GetNextPendingDate(DocumentDatabase database)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.TemporalRevisionsIndex, currentTime, null);

            var qs = string.Format("{0}:{1}", Pending, true);

            var query = new IndexQuery
            {
                Start = 0,
                PageSize = 1,
                Cutoff = currentTime,
                Query = qs,
                FieldsToFetch = new[] { EffectiveStart },
                SortedFields = new[] { new SortedField(EffectiveStart) }
            };

            var result = database.Query(TemporalConstants.TemporalRevisionsIndex, query).Results.FirstOrDefault();
            return result == null ? DateTime.MaxValue : result.Value<DateTime>(EffectiveStart);
        }

        public static IList<string> GetPendingDocumentsRequiringActivation(DocumentDatabase database)
        {
            var currentTime = SystemTime.UtcNow;
            database.WaitForIndexToBecomeNonStale(TemporalConstants.TemporalRevisionsIndex, currentTime, null);

            const int pageSize = 1024;

            var qs = string.Format("{0}:{1} AND {2}:[* TO {3:o}]",
                                   Pending, true,
                                   EffectiveStart, currentTime);

            var query = new IndexQuery
            {
                Start = 0,
                PageSize = pageSize,
                Cutoff = currentTime,
                Query = qs,
                FieldsToFetch = new[] { Constants.DocumentIdFieldName },
                SortedFields = new[] { new SortedField(EffectiveStart) }
            };

            var list = new List<string>();
            while (true)
            {
                var results = database.Query(TemporalConstants.TemporalRevisionsIndex, query).Results;
                list.AddRange(results.Select(x => x.Value<string>(Constants.DocumentIdFieldName)));
                if (results.Count < pageSize)
                    return list;
                query.Start += pageSize;
            }
        }
    }
}
