// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Flags.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

// ReSharper disable once CheckNamespace
namespace BackupManager;

internal static partial class Utils
{
    /// <summary>
    ///     Provides a set of static methods for use with "flags" enums,
    ///     i.e. those decorated with <see cref="FlagsAttribute" />.
    ///     Other than <see cref="IsValidCombination{T}" />, methods in this
    ///     class throw <see cref="TypeArgumentException" />.
    /// </summary>
    public static class Flags
    {
        /// <summary>
        ///     Returns all the bits used in any flag values
        /// </summary>
        /// internal static
        /// <returns>A flag value with all the bits set that are ever set in any defined value</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public static T GetUsedBits<T>() where T : Enum
        {
            ThrowIfNotFlags<T>();
            return EnumInternals<T>.UsedBits;
        }

        /// <summary>
        ///     Helper method used by almost all methods to make sure
        ///     the type argument is really a flags enum.
        /// </summary>
        public static void ThrowIfNotFlags<T>() where T : Enum
        {
            if (!EnumInternals<T>.IsFlags) throw new TypeArgumentException("Can't call this method for a non-flags enum");
        }

        /// <summary>
        ///     Returns whether or not the specified enum is a "flags" enum,
        ///     i.e. whether it has FlagsAttribute applied to it.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>
        ///     True if the enum type is decorated with
        ///     FlagsAttribute; False otherwise.
        /// </returns>
        public static bool IsFlags<T>() where T : Enum
        {
            return EnumInternals<T>.IsFlags;
        }
    }
}