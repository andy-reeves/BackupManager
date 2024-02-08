// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Int64Extensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Globalization;

namespace BackupManager.Extensions;

// ReSharper disable once UnusedType.Global
public static class Int64Extensions
{
    private static readonly string[] _sizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };

    // ReSharper disable once UnusedMember.Global
    public static string SizeSuffix(this long bytes)
    {
        switch (bytes)
        {
            case < 0:
                return "-" + SizeSuffix(-bytes);
            case 0:
                return "0 B";
        }
        var mag = (int)Math.Log(bytes, Utils.BYTES_IN_ONE_KILOBYTE);
        var adjustedSize = bytes / (decimal)Math.Pow(Utils.BYTES_IN_ONE_KILOBYTE, mag);
        return string.Format(CultureInfo.InvariantCulture, "{0:n1} {1}", adjustedSize, _sizeSuffixes[mag]);
    }
}