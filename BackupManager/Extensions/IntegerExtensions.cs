// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="IntegerExtensions.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Extensions
{
    public static class IntegerExtensions
    {
        /// <summary>
        /// Returns an ordinal string of the number.
        /// </summary>
        /// <param name="number">The number.</param>
        /// <returns>Ordinal value of positive integers, or <see cref="int.ToString"/> if less than 1.</returns>
        public static string ToOrdinalString(this int number)
        {
            string numberString = number.ToString();

            return number < 1
                ? numberString
                : numberString.EndsWith("11") || numberString.EndsWith("12") || numberString.EndsWith("13")
                ? numberString + "th"
                : numberString.EndsWith("1")
                ? numberString + "st"
                : numberString.EndsWith("2") ? numberString + "nd" : numberString.EndsWith("3") ? numberString + "rd" : numberString + "th";
        }
    }
}
