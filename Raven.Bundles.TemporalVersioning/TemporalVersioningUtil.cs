using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

        public static TemporalHistory GetTemporalHistoryFor(this DocumentDatabase database, string key, TransactionInformation transactionInformation,
                                                            out Etag etag)
        {
            using (database.DisableAllTriggersForCurrentThread())
            {
                var doc = database.Get(TemporalHistory.GetKeyFor(key), transactionInformation);
                if (doc == null)
                {
                    etag = null;
                    return new TemporalHistory();
                }

                etag = doc.Etag;
                return doc.DataAsJson.JsonDeserialization<TemporalHistory>();
            }
        }

        public static void SaveTemporalHistoryFor(this DocumentDatabase database, string key, TemporalHistory history,
                                                  TransactionInformation transactionInformation, Etag etag)
        {
            using (database.DisableAllTriggersForCurrentThread())
            {
                var document = RavenJObject.FromObject(history);
                var metadata = new RavenJObject();
                database.Put(TemporalHistory.GetKeyFor(key), etag, document, metadata, transactionInformation);
            }
        }

        public static bool IsTemporalVersioningEnabled(this DocumentDatabase database, string key, RavenJObject metadata)
        {
            if (key == null)
                return false;

            // Don't ever version raven system documents.
            if (key.StartsWith("Raven/", StringComparison.InvariantCultureIgnoreCase))
                return false;

            // Don't version this one from the test helpers either.
            if (key == "Pls Delete Me")
                return false;

            // Don't version any doc that isn't an entity.
            var entityName = metadata.Value<string>(Constants.RavenEntityName);
            if (entityName == null)
                return false;

            bool enabled;
            var cacheKey = (database.Name ?? "") + ":" + entityName;
            if (ConfigCache.TryGetValue(cacheKey, out enabled))
                return enabled;

            var temporalVersioningConfiguration = database.GetTemporalVersioningConfiguration(entityName);
            enabled = temporalVersioningConfiguration != null && temporalVersioningConfiguration.Enabled;
            ConfigCache.TryAdd(cacheKey, enabled);

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

        public static void WaitForIndexToBecomeNonStale(this DocumentDatabase database, string name, DateTime? cutOff, Etag cutoffEtag)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (true)
            {
                if (database.Disposed)
                    break;

                var stale = true;
                database.TransactionalStorage.Batch(x => { stale = x.Staleness.IsIndexStale(name, cutOff, cutoffEtag); });

                if (!stale)
                    break;

                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(30))
                    throw new TimeoutException(
                        string.Format("Over 30 seconds have elapsed while waiting for the \"{0}\" index to catch up.", name));

                Thread.Sleep(100);
            }

            stopwatch.Stop();
        }

        public static bool IsBundleActive(this DocumentDatabase database, string bundleName)
        {
            var assembliesLoaded = AppDomain.CurrentDomain.GetAssemblies();
            var embeddedMode = assembliesLoaded.Any(x => x.GetName().Name.Contains("Raven.Client.Embedded"));
            if (embeddedMode)
                return true;

            var activeBundles = database.Configuration.Settings[Constants.ActiveBundles];
            return activeBundles != null && activeBundles.Split(';').Contains(bundleName, StringComparer.OrdinalIgnoreCase);
        }
    }
}
