﻿using System.Threading.Tasks;

namespace Certes.Acme
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IResourceContext<T>
    {
        /// <summary>
        /// Gets the acme resource.
        /// </summary>
        /// <returns></returns>
        Task<T> Resource();
    }
}