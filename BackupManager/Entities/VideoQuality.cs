// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="VideoQuality.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal enum VideoQuality
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "WEBDL")] WEBDL,

    [EnumMember(Value = "HDTV")] HDTV,

    [EnumMember(Value = "SDTV")] SDTV,

    [EnumMember(Value = "WEBRip")] WEBRip,

    [EnumMember(Value = "DVD")] DVD,

    [EnumMember(Value = "Bluray")] Bluray
}