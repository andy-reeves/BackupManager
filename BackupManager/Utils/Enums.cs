// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Enums.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

using BackupManager.Properties;

// ReSharper disable MemberCanBeInternal
// ReSharper disable once CheckNamespace
namespace BackupManager;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static partial class Utils
{
    /// <summary>
    ///     Provides a set of static methods for use with enum types. Much of
    ///     what's available here is already in System.Enum, but this class
    ///     provides a strongly typed API.
    /// </summary>
    public static class Enums
    {
        /// <summary>
        ///     Returns an array of values in the enum.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>An array of values in the enum</returns>
        public static T[] GetValuesArray<T>() where T : Enum
        {
            return (T[])Enum.GetValues(typeof(T));
        }

        /// <summary>
        ///     Returns an array of names in the enum.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>An array of names in the enum</returns>
        public static string[] GetNamesArray<T>() where T : EnumConverter
        {
            return Enum.GetNames(typeof(T));
        }

        /// <summary>
        ///     Returns the names for the given enum as an immutable list.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>An array of names in the enum</returns>
        public static IList<string> GetNames<T>() where T : Enum
        {
            return EnumInternals<T>.Names;
        }

        /// <summary>
        ///     Returns the values for the given enum as an immutable list.
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        public static IList<T> GetValues<T>() where T : Enum
        {
            return EnumInternals<T>.Values;
        }

        /// <summary>
        ///     Attempts to find a value with the given description.
        /// </summary>
        /// <remarks>
        ///     More than one value may have the same description. In this unlikely
        ///     situation, the first value with the specified description is returned.
        /// </remarks>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="description">Description to find</param>
        /// <param name="value">Enum value corresponding to given description (on return)</param>
        /// <returns>
        ///     True if a value with the given description was found,
        ///     false otherwise.
        /// </returns>
        public static bool TryParseDescription<T>(string description, out T value) where T : Enum
        {
            return EnumInternals<T>.DescriptionToValueMap.TryGetValue(description, out value);
        }

        /// <summary>
        ///     Parses the name of an enum value.
        /// </summary>
        /// <remarks>
        ///     This method only considers named values: it does not parse comma-separated
        ///     combinations of flags enums.
        /// </remarks>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>The parsed value</returns>
        /// <exception cref="ArgumentException">The name could not be parsed.</exception>
        public static T ParseName<T>(string name) where T : Enum
        {
            return !TryParseName(name, out T value) ? throw new ArgumentException(Resources.EnumsParseNameUnknownName, nameof(name)) : value;
        }

        /// <summary>
        ///     Attempts to find a value for the specified name.
        ///     Only names are considered - not numeric values.
        /// </summary>
        /// <remarks>
        ///     If the name is not parsed, <paramref name="value" /> will
        ///     be set to the zero value of the enum. This method only
        ///     considers named values: it does not parse comma-separated
        ///     combinations of flags enums.
        /// </remarks>
        /// <typeparam name="T">Enum type</typeparam>
        /// <param name="name">Name to parse</param>
        /// <param name="value">Enum value corresponding to given name (on return)</param>
        /// <returns>Whether the parse attempt was successful or not</returns>
        private static bool TryParseName<T>(string name, out T value) where T : Enum
        {
            var index = EnumInternals<T>.Names.IndexOf(name);

            if (index == -1)
            {
                value = default;
                return false;
            }
            value = EnumInternals<T>.Values[index];
            return true;
        }

        /// <summary>
        ///     Returns the underlying type for the enum
        /// </summary>
        /// <typeparam name="T">Enum type</typeparam>
        /// <returns>The underlying type (Byte, Int32 etc.) for the enum</returns>
        public static Type GetUnderlyingType<T>() where T : Enum
        {
            return EnumInternals<T>.UnderlyingType;
        }
    }
}