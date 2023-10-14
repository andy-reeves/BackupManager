// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="PushoverRetry.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace BackupManager;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
public enum PushoverRetry
{
    None = 0,

    ThirtySeconds = 30,

    OneMinute = 60,

    FiveMinutes = 300,

    ThirtyMinutes = 1800,

    OneHour = 3600
}