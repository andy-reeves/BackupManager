// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRenameRule.cs" company="Andy Reeves">
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
public class FileRenameRule
{
    /// <summary>
    ///     <FileDiscoveryRegEx>^.*\\_TV(\s\(non-tvdb\))?\\.*\.srt$</FileDiscoveryRegEx>
    /// </summary>
    public string FileDiscoveryRegex { get; set; }

    /// <summary>
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// </summary>
    public string Replace { get; set; }
}
