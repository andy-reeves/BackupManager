// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="TvVideoResolution.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
internal enum TvVideoResolution
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "480p")] P480,

    [EnumMember(Value = "576p")] P576,

    [EnumMember(Value = "720p")] P720,

    [EnumMember(Value = "1080p")] P1080,

    [EnumMember(Value = "1080p Remux")] P1080Remux,

    [EnumMember(Value = "2160p")] P2160,

    [EnumMember(Value = "2160p Remux")] P2160Remux
}