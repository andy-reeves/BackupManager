// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="IntegerExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace BackupManager.Extensions;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
internal static class IntegerExtensions
{
    /// <summary>
    ///     Returns an ordinal string of the number.
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>Ordinal value of positive integers, or just a string of the number if less than 1.</returns>
    internal static string ToOrdinalString(this int number)
    {
        var numberString = number.ToString();
        if (number < 1) return numberString;

        if (numberString.EndsWithIgnoreCase("11") || numberString.EndsWithIgnoreCase("12") || numberString.EndsWithIgnoreCase("13"))
            return numberString + "th";
        if (numberString.EndsWithIgnoreCase("1")) return numberString + "st";
        if (numberString.EndsWithIgnoreCase("2")) return numberString + "nd";
        if (numberString.EndsWithIgnoreCase("3")) return numberString + "rd";

        return numberString + "th";
    }
}