// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="DirectoryScan.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
public sealed class DirectoryScan : IEquatable<DirectoryScan>
{
    private TimeSpan scanDuration;

    public DirectoryScan() { }

    public DirectoryScan(DirectoryScanType typeOfScan, string path, DateTime startDateTime, string id)
    {
        TypeOfScan = typeOfScan;
        Path = path;
        StartDateTime = startDateTime;
        Id = id;
    }

    public DirectoryScanType TypeOfScan { get; set; }

    /// <summary>
    ///     The path
    /// </summary>
    public string Path { get; set; }

    private string InternalPath => Path;

    public string Id { get; set; }

    /// <summary>
    /// </summary>
    public DateTime StartDateTime { get; set; }

    /// <summary>
    /// </summary>
    public DateTime EndDateTime { get; set; }

    [XmlIgnore]
    public TimeSpan ScanDuration
    {
        get
        {
            if (EndDateTime.Equals(DateTime.MinValue)) return TimeSpan.Zero;

            scanDuration = EndDateTime - StartDateTime;
            return scanDuration;
        }
    }

    public int TotalFiles { get; set; }

    public bool Equals(DirectoryScan other)
    {
        return null != other && Path == other.Path;
    }

    /// <summary>
    ///     Gets the LapsedTime from a list of DirectoryScans
    /// </summary>
    /// <param name="directoryScans"></param>
    /// <returns></returns>
    internal static TimeSpan LapsedTime(IEnumerable<DirectoryScan> directoryScans)
    {
        var scanStartTime = DateTime.MaxValue;
        var scanEndTime = DateTime.MinValue;

        foreach (var directoryScan in directoryScans)
        {
            if (directoryScan.EndDateTime > scanEndTime) scanEndTime = directoryScan.EndDateTime;
            if (directoryScan.StartDateTime < scanStartTime) scanStartTime = directoryScan.StartDateTime;
        }
        return scanEndTime - scanStartTime;
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as DirectoryScan);
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