using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Database;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning
{
    internal static class TemporalVersioningUtil
    {
        public static readonly ConcurrentDictionary<string, bool> ConfigCache = new ConcurrentDictionary<string, bool>();

        public static TemporalVersioningConfiguration GetTemporalVersioningConfiguration(this DocumentDatabase database, string entityName)
        {
            using (database.DisableAllTriggersForCurrentThread())
            {
                JsonDocument doc = null;

                if (entityName != null)
                    doc = database.Get(string.Format("Raven/{0}/{1}", TemporalConstants.BundleName, entityName), null);

                if (doc == null)
                    doc = database.Get(string.Format("Raven/{0}/DefaultConfiguration", TemporalConstants.BundleName), null);

                return doc == null ? null : doc.DataAsJson.JsonDeserialization<TemporalVersioningConfiguration>();
            }
        }

        public static bool IsTemporalVersioningEnabled(this DocumentDatabase database, string key, RavenJObject metadata)
        {
            // Don't ever version raven system documents.
            if (key != null && key.StartsWith("Raven/", StringComparison.InvariantCultureIgnoreCase))
                return false;

            // Don't version this one from the test helpers either.
            if (key == "Pls Delete Me")
                return false;

            var entityName = metadata.Value<string>(Constants.RavenEntityName);

            bool enabled;
            if (ConfigCache.TryGetValue(entityName, out enabled))
                return enabled;

            var temporalVersioningConfiguration = database.GetTemporalVersioningConfiguration(entityName);
            enabled = temporalVersioningConfiguration != null && temporalVersioningConfiguration.Enabled;
            ConfigCache.TryAdd(entityName, enabled);

            return enabled;
        }

        public static void SetDocumentMetadata(this DocumentDatabase database, string key, TransactionInformation transactionInformation,
                                               string metadataName, RavenJToken metadataValue)
        {
            var metadata = database.GetDocumentMetadata(key, transactionInformation).Metadata;
            metadata[metadataName] = metadataValue;
            database.PutDocumentMetadata(key, metadata);
        }

        public static void SetDocumentMetadata(this DocumentDatabase database, string key, TransactionInformation transactionInformation,
                                               IDictionary<string, RavenJToken> metadataToSet)
        {
            var metadata = database.GetDocumentMetadata(key, transactionInformation).Metadata;
            foreach (var item in metadataToSet)
                metadata[item.Key] = item.Value;
            database.PutDocumentMetadata(key, metadata);
        }

        public static void WaitForIndexToBecomeNonStale(this DocumentDatabase database, string name, DateTime? cutOff, Guid? cutoffEtag)
        {
            // This has to be done on a separate thread ahead of time.  We can't do it at time of query.
            // Because we are in an trigger, the index stats do not get updated if we stay in the same thread.
            // That leads to a condition where the index is not stale, but we still think it is.
            // Waiting in a task on a separate thread works around this problem.
            //
            // Per Oren - this is by design because of esent snapshot isolation.
            // It does work with Munin in Raven >= 2.0.2158, but having it on a separate thread is still ok.
            //
            // http://issues.hibernatingrhinos.com/issue/RavenDB-708
            //

            var task = Task.Factory.StartNew(() =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                while (true)
                {
                    if (database.Disposed)
                        break;

                    var stale = true;
                    database.TransactionalStorage.Batch(x =>
                    {
                        stale = x.Staleness.IsIndexStale(name, cutOff, cutoffEtag);
                    });

                    if (!stale)
                        break;

                    if (stopwatch.Elapsed >= TimeSpan.FromSeconds(30))
                        throw new TimeoutException(
                            string.Format("Over 30 seconds have elapsed while waiting for the \"{0}\" index to catch up.", name));

                    Thread.Sleep(100);
                }

                stopwatch.Stop();
            });
            task.Wait();
        }
    }
}
