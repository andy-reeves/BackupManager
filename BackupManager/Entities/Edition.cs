// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Edition.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "StringLiteralTypo")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "IdentifierTypo")]
internal enum Edition
{
    [EnumMember(Value = "")] Unknown = 0,

    [EnumMember(Value = "10th Anniversary")]
    Anniversary10th,

    [EnumMember(Value = "20th Anniversary")]
    Anniversary20th,

    [EnumMember(Value = "25th Anniversary")]
    Anniversary25th,

    [EnumMember(Value = "30th Anniversary")]
    Anniversary30th,

    [EnumMember(Value = "35th Anniversary")]
    Anniversary35th,

    [EnumMember(Value = "40th Anniversary")]
    Anniversary40th,

    [EnumMember(Value = "45th Anniversary")]
    Anniversary45th,

    [EnumMember(Value = "50th Anniversary")]
    Anniversary50th,

    [EnumMember(Value = "60th Anniversary")]
    Anniversary60th,

    [EnumMember(Value = "70th Anniversary")]
    Anniversary70th,

    [EnumMember(Value = "4K")] FourK,

    [EnumMember(Value = "Bluray")] Bluray,

    [EnumMember(Value = "Chronological")] Chronological,

    [EnumMember(Value = "Collectors")] Collectors,

    [EnumMember(Value = "Criterion Collection")]
    CriterionCollection,

    [EnumMember(Value = "Diamond")] Diamond,

    [EnumMember(Value = "Directors Cut")] DirectorsCut,

    [EnumMember(Value = "DVD")] DVD,

    [EnumMember(Value = "Extended")] Extended,

    [EnumMember(Value = "Final Cut")] FinalCut,

    [EnumMember(Value = "IMAX")] Imax,

    [EnumMember(Value = "KL Studio Collection")]
    KLStudioCollection,

    [EnumMember(Value = "Redux")] Redux,

    [EnumMember(Value = "Remastered")] Remastered,

    [EnumMember(Value = "Restored")] Restored,

    [EnumMember(Value = "Special")] Special,

    [EnumMember(Value = "The Complete Extended")]
    TheCompleteExtended,

    [EnumMember(Value = "The Godfather Coda")]
    TheGodfatherCoda,

    [EnumMember(Value = "The Richard Donner Cut")]
    TheRichardDonnerCut,

    [EnumMember(Value = "Theatrical")] Theatrical,

    [EnumMember(Value = "Ultimate")] Ultimate,

    [EnumMember(Value = "Uncut")] Uncut,

    [EnumMember(Value = "Unrated")] Unrated
}