// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FileSystemEntryType.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager.Entities;

/// <summary>
///     Type of file system object
/// </summary>
internal enum FileSystemEntryType
{
    /// <summary>
    ///     File and Directory do not exist
    /// </summary>
    Missing = 0,

    /// <summary>
    ///     A file.
    /// </summary>
    File,

    /// <summary>
    ///     A directory
    /// </summary>
    Directory
}