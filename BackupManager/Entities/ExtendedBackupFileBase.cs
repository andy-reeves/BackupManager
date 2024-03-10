// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ExtendedBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace BackupManager.Entities;

internal abstract class ExtendedBackupFileBase
{
    protected abstract string DirectoryRegex { get; }

    protected abstract string FileNameRegex { get; }

    protected virtual bool Validate()
    {
        var fileName = GetFileName();
        var regex = new Regex(FileNameRegex);
        IsValid = regex.IsMatch(fileName);
        return IsValid;
    }

    public string GetFileNameWithoutExtension()
    {
        return Path.GetFileNameWithoutExtension(GetFileName());
    }

    public abstract string GetFileName();

    public abstract string GetFullName();

    [XmlIgnore] protected string Extension { get; set; }

    /// <summary>
    ///     Returns True if the current file is valid
    /// </summary>
    [XmlIgnore]
    public bool IsValid { get; protected set; }

    /// <summary>
    ///     The original path and filename of the file before any Refresh may have updated properties
    /// </summary>
    protected string OriginalPath { get; init; }

    /// <summary>
    ///     The title of the file
    /// </summary>
    protected string Title { get; set; }

    /// <summary>
    ///     THe full directory path to the file not including the filename.
    /// </summary>
    public string FullDirectory { get; protected set; }

    public abstract bool RefreshMediaInfo();
}

