using System;
using System.Linq.Expressions;
using Raven.Client.Document;

namespace Raven.Client.Bundles.TemporalVersioning
{
    public class TemporalMultiLoaderWithInclude<T> : ITemporalLoaderWithInclude<T>
    {
        private readonly TemporalSessionOperation _temporal;
        private readonly ILoaderWithInclude<T> _loader;

        internal TemporalMultiLoaderWithInclude(TemporalSessionOperation temporal, ILoaderWithInclude<T> loader)
        {
            _temporal = temporal;
            _loader = loader;
        }

        /// <summary>
		/// Includes the specified path.
		/// </summary>
		/// <param name="path">The path.</param>
		public TemporalMultiLoaderWithInclude<T> Include(string path)
        {
            _loader.Include(path);
			return this;
		}

		/// <summary>
		/// Includes the specified path.
		/// </summary>
		/// <param name="path">The path.</param>
		public TemporalMultiLoaderWithInclude<T> Include(Expression<Func<T, object>> path)
		{
            _loader.Include(path);
            return this;
		}

		public TemporalMultiLoaderWithInclude<T> Include<TInclude>(Expression<Func<T, object>> path)
		{
            _loader.Include(path);
            return this;
		}

		/// <summary>
		/// Loads the specified ids.
		/// </summary>
		/// <param name="ids">The ids.</param>
		public T[] Load(params string[] ids)
		{
		    return _temporal.TemporalLoad(() => _loader.Load<T>(ids));
		}

		/// <summary>
		/// Loads the specified id.
		/// </summary>
		/// <param name="id">The id.</param>
		public T Load(string id)
		{
            return _temporal.TemporalLoad(() => _loader.Load<T>(id));
		}


		/// <summary>
		/// Loads the specified entities with the specified id after applying
		/// conventions on the provided id to get the real document id.
		/// </summary>
		/// <remarks>
		/// This method allows you to call:
		/// Load{Post}(1)
		/// And that call will internally be translated to 
		/// Load{Post}("posts/1");
		/// 
		/// Or whatever your conventions specify.
		/// </remarks>
		public T Load(ValueType id)
		{
            return _temporal.TemporalLoad(() => _loader.Load<T>(id));
		}

		/// <summary>
		/// Loads the specified ids.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="ids">The ids.</param>
		public TResult[] Load<TResult>(params string[] ids)
		{
            return _temporal.TemporalLoad(() => _loader.Load<TResult>(ids));
		}

		/// <summary>
		/// Loads the specified id.
		/// </summary>
		/// <typeparam name="TResult"></typeparam>
		/// <param name="id">The id.</param>
		public TResult Load<TResult>(string id)
		{
            return _temporal.TemporalLoad(() => _loader.Load<TResult>(id));
		}

		/// <summary>
		/// Loads the specified entities with the specified id after applying
		/// conventions on the provided id to get the real document id.
		/// </summary>
		/// <remarks>
		/// This method allows you to call:
		/// Load{Post}(1)
		/// And that call will internally be translated to 
		/// Load{Post}("posts/1");
		/// 
		/// Or whatever your conventions specify.
		/// </remarks>
		public TResult Load<TResult>(ValueType id)
		{
            return _temporal.TemporalLoad(() => _loader.Load<TResult>(id));
		}
    }
}