// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="DateTimeExtensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace BackupManager.Extensions;

internal static class DateTimeExtensions
{
    /// <summary>
    ///     Convert datetime to UNIX time
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>

    // ReSharper disable once UnusedMember.Global
    internal static string ToUnixTime(this DateTime dateTime)
    {
        var dto = new DateTimeOffset(dateTime.ToUniversalTime());
        return dto.ToUnixTimeSeconds().ToString();
    }

    /// <summary>
    ///     Convert datetime to UNIX time including milliseconds
    /// </summary>
    /// <param name="dateTime"></param>
    /// <returns></returns>
    internal static string ToUnixTimeMilliseconds(this DateTime dateTime)
    {
        var dto = new DateTimeOffset(dateTime.ToUniversalTime());
        return dto.ToUnixTimeMilliseconds().ToString();
    }
}