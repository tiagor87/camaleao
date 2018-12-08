﻿namespace Camaleao.Infrastructure.Adapter.Seedwork {

    /// <summary>
    /// Base contract for adapter factory
    /// </summary>
    public interface ITypeAdapterFactory {

        /// <summary>
        /// Create a type adater
        /// </summary>
        /// <returns>The created ITypeAdapter</returns>
        ITypeAdapter Create();
    }
}
