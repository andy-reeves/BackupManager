// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemEntry.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;

namespace BackupManager.Entities;

/// <summary>
///     This class allows us to keep a Collection of FileSystemEntry with the path and datetime it was last changed
/// </summary>
[SuppressMessage("ReSharper", "MemberCanBeInternal")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
[SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "CollectionNeverUpdated.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class FileSystemEntry : IEquatable<FileSystemEntry>
{
    private readonly string path;

    private readonly DateTime modifiedDateTime;

    public FileSystemEntry() { }

    public FileSystemEntry(string path) : this(path, DateTime.Now) { }

    public FileSystemEntry(string path, DateTime dateTime)
    {
        Path = path;
        ModifiedDateTime = dateTime;
    }

    /// <summary>
    ///     The path to the FileSystemEntry that changed
    /// </summary>
    public string Path
    {
        get => path;

        init
        {
            if (path == value) return;

            path = value;
            Changed = true;
        }
    }

    /// <summary>
    ///     This is set to True if any data has changed, and we need to Save.
    /// </summary>
    [XmlIgnore]
    public bool Changed { get; set; }

    private string InternalPath => Path;

    /// <summary>
    ///     The DateTime the FileSystemEntry was last changed
    /// </summary>
    public DateTime ModifiedDateTime
    {
        get => modifiedDateTime;

        init
        {
            if (modifiedDateTime == value) return;

            modifiedDateTime = value;
            Changed = true;
        }
    }

    public bool Equals(FileSystemEntry other)
    {
        return null != other && Path == other.Path;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as FileSystemEntry);
    }

    public override int GetHashCode()
    {
        return InternalPath.GetHashCode();
    }

    public override string ToString()
    {
        return Path;
    }
}