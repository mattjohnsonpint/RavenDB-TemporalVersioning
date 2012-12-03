using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Bundles.TemporalVersioning.Data;
using Raven.Database;
using Raven.Json.Linq;

namespace Raven.Bundles.TemporalVersioning
{
    internal static class TemporalVersioningUtil
    {
        public static TemporalVersioningConfiguration GetTemporalVersioningConfiguration(this DocumentDatabase database, RavenJObject metadata)
        {
            JsonDocument doc = null;

            var entityName = metadata.Value<string>(Constants.RavenEntityName);
            if (entityName != null)
                doc = database.Get(string.Format("Raven/{0}/{1}", TemporalConstants.BundleName, entityName), null);

            if (doc == null)
                doc = database.Get(string.Format("Raven/{0}/DefaultConfiguration", TemporalConstants.BundleName), null);

            return doc == null ? null : doc.DataAsJson.JsonDeserialization<TemporalVersioningConfiguration>();
        }

        public static bool IsTemporalVersioningEnabled(this DocumentDatabase database, string key, RavenJObject metadata)
        {
            // Don't ever version raven system documents.
            if (key.StartsWith("Raven/", StringComparison.InvariantCultureIgnoreCase))
                return false;
            
            var temporalVersioningConfiguration = database.GetTemporalVersioningConfiguration(metadata);
            return temporalVersioningConfiguration != null && temporalVersioningConfiguration.Enabled &&
                   metadata.GetTemporalMetadata().Status != TemporalStatus.NonTemporal;
        }
        
        public static void SetDocumentMetadata(this DocumentDatabase database, string key, TransactionInformation transactionInformation, string metadataName,
                                                 RavenJToken metadataValue)
        {
            // When RavenDB-747 is resolved, replace the implementation with the commented one.
            // http://issues.hibernatingrhinos.com/issue/RavenDB-747

            //var metadata = database.GetDocumentMetadata(key, transactionInformation).Metadata;
            //metadata[metadataName] = metadataValue;
            //database.PutDocumentMetadata(key, metadata);

            var doc = database.Get(key, transactionInformation);
            doc.Metadata[metadataName] = metadataValue;
            database.Put(key, null, doc.DataAsJson, doc.Metadata, transactionInformation);
        }

        public static void WaitForIndexToBecomeNonStale(this DocumentDatabase database, string name, DateTime? cutOff, Guid? cutoffEtag)
        {
            // This has to be done on a separate thread ahead of time.  We can't do it at time of query.
            // Because we are in an trigger, the index stats do not get updated if we stay in the same thread.
            // That leads to a condition where the index is not stale, but we still think it is.
            // Waiting in a task on a separate thread works around this problem.
            // http://issues.hibernatingrhinos.com/issue/RavenDB-708

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
                            string.Format("Over 30 seconds have elpased while waiting for the \"{0}\" index to catch up.", name));

                    Thread.Sleep(100);
                }

                stopwatch.Stop();
            });
            task.Wait();
        }
    }
}
