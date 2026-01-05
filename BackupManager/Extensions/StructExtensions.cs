// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="StructExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;

using static BackupManager.Utils;

namespace BackupManager.Extensions;

[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal static class StructExtensions
{
    /// <param name="value">Value to test</param>
    /// <typeparam name="T">Enum type</typeparam>
    extension<T>(T value) where T : Enum
    {
        /// <summary>
        ///     Checks whether the value is a named value for the type.
        /// </summary>
        /// <remarks>
        ///     For flags enums, it is possible for a value to be a valid
        ///     combination of other values without being a named value
        ///     in itself. To test for this possibility, use IsValidCombination.
        /// </remarks>
        /// <returns>True if this value has a name, False otherwise.</returns>
        public bool IsNamedValue()
        {
            return Enums.GetValues<T>().Contains(value);
        }

        /// <summary>
        ///     Returns the description for the given value,
        ///     as specified by DescriptionAttribute, or null
        ///     if no description is present.
        /// </summary>
        /// <returns>
        ///     The description of the value, or null if no description
        ///     has been specified (but the value is a named value).
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     <paramref name="value" />
        ///     is not a named member of the enum
        /// </exception>
        public string GetDescription()
        {
            return EnumInternals<T>.ValueToDescriptionMap.TryGetValue(value, out var description) ? description : throw new ArgumentOutOfRangeException(nameof(value));
        }

        /// <summary>
        ///     Determines whether the given value only uses bits covered
        ///     by named values.
        /// </summary>
        /// internal static
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public bool IsValidCombination()
        {
            Flags.ThrowIfNotFlags<T>();
            return value.And(EnumInternals<T>.UnusedBits).IsEmpty();
        }

        /// <summary>
        ///     Determines whether the two specified values have any flags in common.
        /// </summary>
        /// <param name="desiredFlags">Flags we wish to find</param>
        /// <returns>Whether the two specified values have any flags in common.</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public bool HasAny(T desiredFlags)
        {
            Flags.ThrowIfNotFlags<T>();
            return value.And(desiredFlags).IsNotEmpty();
        }

        /// <summary>
        ///     Determines whether all flags in <paramref name="desiredFlags" /> are set.
        /// </summary>
        /// <param name="desiredFlags">Flags we wish to find</param>
        /// <returns>Whether all the flags in <paramref name="desiredFlags" /> are in <paramref name="value" />.</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public bool HasAll(T desiredFlags)
        {
            Flags.ThrowIfNotFlags<T>();
            return EnumInternals<T>.Equality(value.And(desiredFlags), desiredFlags);
        }

        /// <summary>
        ///     Returns the bitwise "and" of two values.
        /// </summary>
        /// internal static
        /// <param name="second">Second value</param>
        /// <returns>The bitwise "and" of the two values</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public T And(T second)
        {
            Flags.ThrowIfNotFlags<T>();
            return EnumInternals<T>.And(value, second);
        }

        /// <summary>
        ///     Returns the bitwise "or" of two values.
        /// </summary>
        /// internal static
        /// <param name="second">Second value</param>
        /// <returns>The bitwise "or" of the two values</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public T Or(T second)
        {
            Flags.ThrowIfNotFlags<T>();
            return EnumInternals<T>.Or(value, second);
        }

        /// <summary>
        ///     Returns the inverse of a value, with no consideration for which bits are used
        ///     by values within the enum (i.e. a simple bitwise negation).
        /// </summary>
        /// <returns>The bitwise negation of the value</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public T AllBitsInverse()
        {
            Flags.ThrowIfNotFlags<T>();
            return EnumInternals<T>.Not(value);
        }

        /// <summary>
        ///     Returns the inverse of a value, but limited to those bits which are used by
        ///     values within the enum.
        /// </summary>
        /// <returns>The restricted inverse of the value</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public T UsedBitsInverse()
        {
            Flags.ThrowIfNotFlags<T>();
            return value.AllBitsInverse().And(EnumInternals<T>.UsedBits);
        }

        /// <summary>
        ///     Returns whether this value is an empty set of fields, i.e. the zero value.
        /// </summary>
        /// <returns>True if the value is empty (zero); False otherwise.</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public bool IsEmpty()
        {
            Flags.ThrowIfNotFlags<T>();
            return EnumInternals<T>.IsEmpty(value);
        }

        /// <summary>
        ///     Returns whether this value has any fields set, i.e. is not zero.
        /// </summary>
        /// <returns>True if the value is non-empty (not zero); False otherwise.</returns>
        /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
        public bool IsNotEmpty()
        {
            Flags.ThrowIfNotFlags<T>();
            return !value.IsEmpty();
        }
    }
}