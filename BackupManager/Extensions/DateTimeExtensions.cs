// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="DateTimeExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace BackupManager.Extensions;

internal static class DateTimeExtensions
{
    /// <param name="dateTime"></param>
    extension(DateTime dateTime)
    {
        /// <summary>
        ///     Convert datetime to UNIX time
        /// </summary>
        /// <returns></returns>

        // ReSharper disable once UnusedMember.Global
        internal string ToUnixTime()
        {
            var dto = new DateTimeOffset(dateTime.ToUniversalTime());
            return dto.ToUnixTimeSeconds().ToString();
        }

        /// <summary>
        ///     Convert datetime to UNIX time including milliseconds
        /// </summary>
        /// <returns></returns>
        internal string ToUnixTimeMilliseconds()
        {
            var dto = new DateTimeOffset(dateTime.ToUniversalTime());
            return dto.ToUnixTimeMilliseconds().ToString();
        }
    }
}