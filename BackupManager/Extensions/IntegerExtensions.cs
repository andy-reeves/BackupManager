namespace BackupManager
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

            if (number < 1) return numberString;
            if (numberString.EndsWith("11") || numberString.EndsWith("12") || numberString.EndsWith("13")) return numberString + "th";
            if (numberString.EndsWith("1")) return numberString + "st";
            if (numberString.EndsWith("2")) return numberString + "nd";
            if (numberString.EndsWith("3")) return numberString + "rd";

            return numberString + "th";
        }
    }
}
