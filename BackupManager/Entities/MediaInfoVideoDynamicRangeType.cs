// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="MediaInfoVideoDynamicRangeType.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
internal enum MediaInfoVideoDynamicRangeType
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "DV HDR10")] DV_HDR10,

    [EnumMember(Value = "DV HDR10Plus")] DVHDR10Plus,

    [EnumMember(Value = "HDR10Plus")] HDR10Plus,

    [EnumMember(Value = "HDR10")] HDR10,

    [EnumMember(Value = "HLG")] HLG,

    [EnumMember(Value = "PQ")] PQ
}