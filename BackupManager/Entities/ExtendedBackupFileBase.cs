// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ExtendedBackupFileBase.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.IO;
using System.Text.RegularExpressions;

using BackupManager.Radarr;

namespace BackupManager.Entities;

internal abstract class ExtendedBackupFileBase
{
    // ReSharper disable once UnusedMemberInSuper.Global
    protected abstract string DirectoryRegex { get; }

    protected abstract string FileNameRegex { get; }

    public MediaInfoModel MediaInfoModel { get; protected set; }

    protected string Extension { get; set; }

    /// <summary>
    ///     Returns True if the current file has a valid file name and a valid directory name
    /// </summary>
    public bool IsValid => IsValidFileName && IsValidDirectoryName;

    /// <summary>
    ///     Returns True if the current file has a valid file name. AVC and x264, x265 are valid
    /// </summary>
    public bool IsValidFileName { get; protected set; }

    /// <summary>
    ///     Returns True if the current file has a valid directory name
    /// </summary>
    public bool IsValidDirectoryName { get; protected set; }

    /// <summary>
    ///     The original path and filename of the file before any Refresh may have updated properties
    /// </summary>
    protected string OriginalPath { get; init; }

    /// <summary>
    ///     The title of the file
    /// </summary>
    public string Title { get; protected set; }

    /// <summary>
    ///     A string representing the directory's full path not including the file name.
    /// </summary>
    public string DirectoryName { get; protected set; }

    // ReSharper disable once VirtualMemberNeverOverridden.Global
    protected virtual bool Validate()
    {
        var fileName = GetFileName();
        IsValidFileName = new Regex(FileNameRegex).IsMatch(fileName);
        IsValidDirectoryName = new Regex(DirectoryRegex).IsMatch(DirectoryName);
        return IsValidFileName && IsValidDirectoryName;
    }

    /// <summary>
    ///     Returns the file name without the extension.
    /// </summary>
    /// <returns>
    ///     The string returned by System.IO.Path.GetFileName(System.ReadOnlySpan{System.Char}),
    ///     minus the last period (.) and all characters following it.
    /// </returns>
    public string GetFileNameWithoutExtension()
    {
        return Path.GetFileNameWithoutExtension(GetFileName());
    }

    public abstract string GetFileName();

    /// <summary>
    ///     The full path name to the video file e.g. Directory plus FileName with extension
    /// </summary>
    /// <returns></returns>
    public abstract string GetFullName();

    public abstract bool RefreshMediaInfo();
}