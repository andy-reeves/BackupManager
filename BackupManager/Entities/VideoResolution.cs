// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoResolution.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
internal enum VideoResolution
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "480p")] R480p = 480,

    [EnumMember(Value = "576p")] R576p = 576,

    [EnumMember(Value = "720p")] R720p = 720,

    [EnumMember(Value = "1080p")] R1080p = 1080,

    [EnumMember(Value = "2160p")] R2160p = 2160
}