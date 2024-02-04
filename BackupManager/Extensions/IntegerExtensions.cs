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

        return number < 1 ? numberString :
            numberString.EndsWithIgnoreCase("11") || numberString.EndsWithIgnoreCase("12") ||
            numberString.EndsWithIgnoreCase("13") ? numberString + "th" :
            numberString.EndsWithIgnoreCase("1") ? numberString + "st" :
            numberString.EndsWithIgnoreCase("2") ? numberString + "nd" :
            numberString.EndsWithIgnoreCase("3") ? numberString + "rd" : numberString + "th";
    }
}