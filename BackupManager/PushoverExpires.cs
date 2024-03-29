﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="PushoverExpires.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace BackupManager;

[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum PushoverExpires
{
    Immediately = 0,

    ThirtySeconds = 30,

    OneMinute = 60,

    FiveMinutes = 300,

    ThirtyMinutes = 1800,

    OneHour = 3600
}