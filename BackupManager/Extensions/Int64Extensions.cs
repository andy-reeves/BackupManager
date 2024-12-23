﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Int64Extensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Globalization;

namespace BackupManager.Extensions;

// ReSharper disable once UnusedType.Global
internal static class Int64Extensions
{
    private static readonly string[] _sizeSuffixes = ["bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"];

    /// <summary>
    ///     Returns a byte value provided with the correct size suffix like x bytes, 23 KB, etc.
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>

    // ReSharper disable once UnusedMember.Global
    internal static string SizeSuffix(this long bytes)
    {
        switch (bytes)
        {
            case < 0:
                return "-" + SizeSuffix(-bytes);
            case 0:
                return "0 bytes";
        }
        var mag = (int)Math.Log(bytes, Utils.BYTES_IN_ONE_KILOBYTE);
        var adjustedSize = bytes / (decimal)Math.Pow(Utils.BYTES_IN_ONE_KILOBYTE, mag);

        return Utils.IsWholeNumber(adjustedSize, 1)
            ? string.Format(CultureInfo.InvariantCulture, "{0:n0} {1}", adjustedSize, _sizeSuffixes[mag])
            : string.Format(CultureInfo.InvariantCulture, mag == 0 ? "{0:n0} {1}" : "{0:n1} {1}", adjustedSize, _sizeSuffixes[mag]);
    }
}