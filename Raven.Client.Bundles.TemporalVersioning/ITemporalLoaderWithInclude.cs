using System;
using System.Linq.Expressions;

namespace Raven.Client.Bundles.TemporalVersioning
{
    public interface ITemporalLoaderWithInclude<T>
    {
        /// <summary>
        /// Includes the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        TemporalMultiLoaderWithInclude<T> Include(string path);

        /// <summary>
        /// Includes the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        TemporalMultiLoaderWithInclude<T> Include(Expression<Func<T, object>> path);

        /// <summary>
        /// Includes the specified path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        TemporalMultiLoaderWithInclude<T> Include<TInclude>(Expression<Func<T, object>> path);

        /// <summary>
        /// Loads the specified ids.
        /// </summary>
        /// <param name="ids">The ids.</param>
        /// <returns></returns>
        T[] Load(params string[] ids);

        /// <summary>
        /// Loads the specified id.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <returns></returns>
        T Load(string id);

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
        T Load(ValueType id);

        /// <summary>
        /// Loads the specified ids.
        /// </summary>
        /// <param name="ids">The ids.</param>
        /// <returns></returns>
        TResult[] Load<TResult>(params string[] ids);

        /// <summary>
        /// Loads the specified id.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="id">The id.</param>
        /// <returns></returns>
        TResult Load<TResult>(string id);

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
        TResult Load<TResult>(ValueType id);
    }
}