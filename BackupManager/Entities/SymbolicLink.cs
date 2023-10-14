// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="SymbolicLink.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class SymbolicLink
{
    public string RootFolder { get; set; }

    public string FileDiscoveryRegEx { get; set; }

    public string RelativePath { get; set; }

    public string PathToTarget { get; set; }
}