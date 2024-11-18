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
    /// <summary>
    ///     Checks whether the value is a named value for the type.
    /// </summary>
    /// <remarks>
    ///     For flags enums, it is possible for a value to be a valid
    ///     combination of other values without being a named value
    ///     in itself. To test for this possibility, use IsValidCombination.
    /// </remarks>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to test</param>
    /// <returns>True if this value has a name, False otherwise.</returns>
    public static bool IsNamedValue<T>(this T value) where T : Enum
    {
        // TODO: Speed this up for big enums
        return Enums.GetValues<T>().Contains(value);
    }

    /// <summary>
    ///     Returns the description for the given value,
    ///     as specified by DescriptionAttribute, or null
    ///     if no description is present.
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="item">Value to fetch description for</param>
    /// <returns>
    ///     The description of the value, or null if no description
    ///     has been specified (but the value is a named value).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="item" />
    ///     is not a named member of the enum
    /// </exception>
    public static string GetDescription<T>(this T item) where T : Enum
    {
        if (EnumInternals<T>.ValueToDescriptionMap.TryGetValue(item, out var description)) return description;

        throw new ArgumentOutOfRangeException(nameof(item));
    }

    /// <summary>
    ///     Determines whether the given value only uses bits covered
    ///     by named values.
    /// </summary>
    /// internal static
    /// <param name="values">Combination to test</param>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static bool IsValidCombination<T>(this T values) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return values.And(EnumInternals<T>.UnusedBits).IsEmpty();
    }

    /// <summary>
    ///     Determines whether the two specified values have any flags in common.
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <param name="desiredFlags">Flags we wish to find</param>
    /// <returns>Whether the two specified values have any flags in common.</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static bool HasAny<T>(this T value, T desiredFlags) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return value.And(desiredFlags).IsNotEmpty();
    }

    /// <summary>
    ///     Determines whether all of the flags in <paramref name="desiredFlags" />
    /// </summary>
    /// <param name="value">Value to test</param>
    /// <param name="desiredFlags">Flags we wish to find</param>
    /// <returns>Whether all the flags in <paramref name="desiredFlags" /> are in <paramref name="value" />.</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static bool HasAll<T>(this T value, T desiredFlags) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return EnumInternals<T>.Equality(value.And(desiredFlags), desiredFlags);
    }

    /// <summary>
    ///     Returns the bitwise "and" of two values.
    /// </summary>
    /// internal static
    /// <param name="first">First value</param>
    /// <param name="second">Second value</param>
    /// <returns>The bitwise "and" of the two values</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static T And<T>(this T first, T second) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return EnumInternals<T>.And(first, second);
    }

    /// <summary>
    ///     Returns the bitwise "or" of two values.
    /// </summary>
    /// internal static
    /// <param name="first">First value</param>
    /// <param name="second">Second value</param>
    /// <returns>The bitwise "or" of the two values</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static T Or<T>(this T first, T second) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return EnumInternals<T>.Or(first, second);
    }

    /// <summary>
    ///     Returns the inverse of a value, with no consideration for which bits are used
    ///     by values within the enum (i.e. a simple bitwise negation).
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to invert</param>
    /// <returns>The bitwise negation of the value</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static T AllBitsInverse<T>(this T value) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return EnumInternals<T>.Not(value);
    }

    /// <summary>
    ///     Returns the inverse of a value, but limited to those bits which are used by
    ///     values within the enum.
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to invert</param>
    /// <returns>The restricted inverse of the value</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static T UsedBitsInverse<T>(this T value) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return value.AllBitsInverse().And(EnumInternals<T>.UsedBits);
    }

    /// <summary>
    ///     Returns whether this value is an empty set of fields, i.e. the zero value.
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to test</param>
    /// <returns>True if the value is empty (zero); False otherwise.</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static bool IsEmpty<T>(this T value) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return EnumInternals<T>.IsEmpty(value);
    }

    /// <summary>
    ///     Returns whether this value has any fields set, i.e. is not zero.
    /// </summary>
    /// <typeparam name="T">Enum type</typeparam>
    /// <param name="value">Value to test</param>
    /// <returns>True if the value is non-empty (not zero); False otherwise.</returns>
    /// <exception cref="TypeArgumentException"><typeparamref name="T" /> is not a flags enum.</exception>
    public static bool IsNotEmpty<T>(this T value) where T : Enum
    {
        Flags.ThrowIfNotFlags<T>();
        return !value.IsEmpty();
    }
}