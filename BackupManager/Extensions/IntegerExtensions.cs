// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="IntegerExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
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
            numberString.EndsWith("11", StringComparison.Ordinal) || numberString.EndsWith("12", StringComparison.Ordinal) ||
            numberString.EndsWith("13", StringComparison.Ordinal) ? numberString + "th" :
            numberString.EndsWith("1", StringComparison.Ordinal) ? numberString + "st" :
            numberString.EndsWith("2", StringComparison.Ordinal) ? numberString + "nd" :
            numberString.EndsWith("3", StringComparison.Ordinal) ? numberString + "rd" : numberString + "th";
    }
}