using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq.Expressions;
using Raven.Client.Bundles.TemporalVersioning.Common;
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

        public IRavenQueryable<T> Query<T>()
        {
            return _session.Query<T>().Customize(IncludeTemporalEffectiveDateOnQuery());
        }

        public IRavenQueryable<T> Query<T>(string indexName)
        {
            return _session.Query<T>(indexName).Customize(IncludeTemporalEffectiveDateOnQuery());
        }

        public IRavenQueryable<T> Query<T, TIndexCreator>()
            where TIndexCreator : AbstractIndexCreationTask, new()
        {
            return _session.Query<T, TIndexCreator>().Customize(IncludeTemporalEffectiveDateOnQuery());
        }

        private Action<IDocumentQueryCustomization> IncludeTemporalEffectiveDateOnQuery()
        {
            // This gets stripped out later by the listener
            return x => x.Include("__TemporalEffectiveDate__=" + _effectiveDate.UtcDateTime.ToString("o"));
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
            PrepareNewRevision(entity);
        }

        public void Store(object entity, Guid etag, string id)
        {
            _session.Store(entity, etag, id);
            PrepareNewRevision(entity);
        }

        public void Store(dynamic entity)
        {
            _session.Store(entity);
            PrepareNewRevision(entity);
        }

        public void Store(dynamic entity, string id)
        {
            _session.Store(entity, id);
            PrepareNewRevision(entity);
        }

        private void PrepareNewRevision(object entity)
        {
            var temporal = _session.Advanced.GetTemporalMetadataFor(entity);
            temporal.Status = TemporalStatus.Revision;
            temporal.Effective = _effectiveDate.UtcDateTime;
        }

        #endregion

        internal T TemporalLoad<T>(Func<T> loadOperation)
        {
            // perform the load operation, passing the temporal effective date header just for this operation
            _headers.Add(TemporalConstants.TemporalEffectiveDate, _effectiveDate.UtcDateTime.ToString("o"));
            var result = loadOperation();
            _headers.Remove(TemporalConstants.TemporalEffectiveDate);

            return result;
        }
    }
}
