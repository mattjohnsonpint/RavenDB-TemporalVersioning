using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Linq.Expressions;
using Raven.Bundles.TemporalVersioning.Common;
using Raven.Client.Document;
using Raven.Client.Indexes;
using Raven.Client.Linq;

namespace Raven.Client.Bundles.TemporalVersioning
{
    public class TemporalSessionOperation : ISyncTemporalSessionOperation
    {
        private readonly IDocumentSession _session;
        private readonly DateTimeOffset _effectiveDate;
        private readonly NameValueCollection _headers;

        internal TemporalSessionOperation(IDocumentSession session, DateTimeOffset effectiveDate)
        {
            _session = session;
            _effectiveDate = effectiveDate;
            _headers = ((DocumentSession) _session).DatabaseCommands.OperationsHeaders;
        }

        #region Load

        public T Load<T>(string id)
        {
            return TemporalLoad(() => _session.Load<T>(id));
        }

        public T[] Load<T>(params string[] ids)
        {
            return TemporalLoad(() => _session.Load<T>(ids));
        }

        public T[] Load<T>(IEnumerable<string> ids)
        {
            return TemporalLoad(() => _session.Load<T>(ids));
        }

        public T Load<T>(ValueType id)
        {
            return TemporalLoad(() => _session.Load<T>(id));
        }

        #endregion

        #region Query

        public IRavenQueryable<T> Query<T>(string indexName)
        {
            _headers[TemporalConstants.EffectiveDateHeader] = _effectiveDate.ToString("o");
            return _session.Query<T>(indexName);
        }

        public IRavenQueryable<T> Query<T>()
        {
            _headers[TemporalConstants.EffectiveDateHeader] = _effectiveDate.ToString("o");
            return _session.Query<T>();
        }

        public IRavenQueryable<T> Query<T, TIndexCreator>()
            where TIndexCreator : AbstractIndexCreationTask, new()
        {
            _headers[TemporalConstants.EffectiveDateHeader] = _effectiveDate.ToString("o");
            return _session.Query<T, TIndexCreator>();
        }

        #endregion

        #region Include

        public ITemporalLoaderWithInclude<object> Include(string path)
        {
            return new TemporalMultiLoaderWithInclude<object>(this, _session.Include(path));
        }

        public ITemporalLoaderWithInclude<T> Include<T>(Expression<Func<T, object>> path)
        {
            return new TemporalMultiLoaderWithInclude<T>(this, _session.Include(path));
        }

        public ITemporalLoaderWithInclude<T> Include<T, TInclude>(Expression<Func<T, object>> path)
        {
            return new TemporalMultiLoaderWithInclude<T>(this, _session.Include<T, TInclude>(path));
        }

        #endregion

        #region Store

        public void Store(object entity, Guid etag)
        {
            _session.Store(entity, etag);
            _session.PrepareNewRevision(entity, _effectiveDate);
        }

        public void Store(object entity, Guid etag, string id)
        {
            _session.Store(entity, etag, id);
            _session.PrepareNewRevision(id, _effectiveDate);
        }

        public void Store(dynamic entity)
        {
            _session.Store(entity);
            _session.PrepareNewRevision((object) entity, _effectiveDate);
        }

        public void Store(dynamic entity, string id)
        {
            _session.Store(entity, id);
            _session.PrepareNewRevision(id, _effectiveDate);
        }

        #endregion

        #region Delete

        public void Delete<T>(T entity)
        {
            // Deletions have to send the effective date in a header keyed by the document id
            var key = _session.Advanced.GetDocumentId(entity);
            var headers = ((DocumentSession) _session).DatabaseCommands.OperationsHeaders;
            var header = String.Format("{0}-{1}", TemporalConstants.EffectiveDateHeader, key.Replace('/', '-'));
            headers[header] = _effectiveDate.ToString("o");

            _session.Delete(entity);
        }

        #endregion

        internal T TemporalLoad<T>(Func<T> loadOperation)
        {
            // set aside any other temporal headers that were set on the session
            var temp = _headers.AllKeys
                               .Where(x => x.StartsWith("Temporal"))
                               .ToDictionary(x => x, x => _headers[x]);
            foreach (var header in temp)
                _headers.Remove(header.Key);

            // perform the load operation, passing the temporal effective date header just for this operation
            _headers.Add(TemporalConstants.EffectiveDateHeader, _effectiveDate.ToString("o"));
            var result = loadOperation();
            _headers.Remove(TemporalConstants.EffectiveDateHeader);

            // restore any headers that were removed above
            foreach (var header in temp)
                _headers.Add(header.Key, header.Value);

            return result;
        }
    }
}
