// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemEntry.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;

namespace BackupManager.Entities;

/// <summary>
///     This class allows us to keep a Collection of FileSystemEntry with the path and datetime it was last changed
/// </summary>
public class FileSystemEntry : IEquatable<FileSystemEntry>
{
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

    public string Path { get; set; }

    private string InternalPath => Path;

    /// <summary>
    ///     The DateTime the FileSystemEntry was last changed
    /// </summary>
    public DateTime ModifiedDateTime { get; set; }

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