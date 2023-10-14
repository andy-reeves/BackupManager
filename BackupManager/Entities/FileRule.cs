// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileRule.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace BackupManager.Entities;

[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[DebuggerDisplay("Message = {Message}")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class FileRule : IEquatable<FileRule>
{
    /// <summary>
    ///     We use this to track if the rule has been used for any files at all in a full scan
    /// </summary>
    [XmlIgnore] internal bool Matched;

    /// <summary>
    ///     If the file path matches this then it must match FileTestRegEx
    /// </summary>
    public string FileDiscoveryRegEx { get; set; }

    /// <summary>
    ///     If the file path matches FileDiscoveryRegEx then it must match this
    /// </summary>
    public string FileTestRegEx { get; set; }

    /// <summary>
    ///     The message to display if the rule is not matched
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    ///     The name of the rule. Must be unique
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     The number of the rule. Must be unique
    /// </summary>
    public string Number { get; set; }

    private string InternalName => Name;

    private string InternalNumber => Number;

    public bool Equals(FileRule other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Name == other.Name && Number == other.Number;
    }

    public override bool Equals(object obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;

        return obj.GetType() == GetType() && Equals((FileRule)obj);
    }

    public override string ToString()
    {
        return $"{Name} {Message}";
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(InternalName, InternalNumber);
    }
}