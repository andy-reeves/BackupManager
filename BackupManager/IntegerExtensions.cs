using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackupManager
{
    public static class IntegerExtensions
    {
        /// <summary>
        /// Returns an ordinal string of the number.
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public static string ToOrdinalString(this int num)
        {
            string number = num.ToString();

            if (num <= 0) return string.Empty;
            if (number.EndsWith("11") || number.EndsWith("12") || number.EndsWith("13")) return number + "th";
            if (number.EndsWith("1")) return number + "st";
            if (number.EndsWith("2")) return number + "nd";
            if (number.EndsWith("3")) return number + "rd";

            return number + "th";
        }
    }
}
