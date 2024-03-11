// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MovieVideoResolution.cs" company="Andy Reeves">
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
internal enum MovieVideoResolution
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "480p")] P480,

    [EnumMember(Value = "576p")] P576,

    [EnumMember(Value = "720p")] P720,

    [EnumMember(Value = "1080p")] P1080,

    [EnumMember(Value = "2160p")] P2160
}