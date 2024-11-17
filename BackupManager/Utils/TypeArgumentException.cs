// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TypeArgumentException.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

// ReSharper disable once CheckNamespace
namespace BackupManager;

internal static partial class Utils
{
    /// <summary>
    ///     Exception thrown to indicate that an inappropriate type argument was used for
    ///     a type parameter to a generic type or method.
    /// </summary>
    public sealed class TypeArgumentException : Exception
    {
        /// <summary>
        ///     Constructs a new instance of TypeArgumentException with the given message.
        /// </summary>
        /// <param name="message">Message for the exception.</param>
        public TypeArgumentException(string message) : base(message) { }
    }
}