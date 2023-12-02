// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.Scanning.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    /// <summary>
    ///     Scan the directory provided.
    /// </summary>
    /// <param name="directoryToCheck">The full path to scan</param>
    /// <param name="searchOption">Whether to search subdirectories</param>
    /// <returns>True if the scan was successful otherwise False. Returns True if the directory doesn't exist</returns>
    private bool ScanSingleDirectory(string directoryToCheck, SearchOption searchOption)
    {
        Utils.TraceIn(directoryToCheck, searchOption);
        if (!Directory.Exists(directoryToCheck)) return Utils.TraceOut(true);

        var subDirectoryText = searchOption == SearchOption.TopDirectoryOnly ? "directory only" : "and subdirectories";
        Utils.LogWithPushover(BackupAction.ScanDirectory, $"{directoryToCheck}");
        Utils.Trace($"{directoryToCheck} {subDirectoryText}");
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck));
        var filters = mediaBackup.GetFilters();
        var files = Utils.GetFiles(directoryToCheck, filters, searchOption);
        EnableProgressBar(0, files.Length);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            Utils.Trace($"Checking {file}");
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck), i + 1);
            if (CheckForFilesToDelete(file)) continue;

            // RegEx file name rules
            foreach (var rule in mediaBackup.Config.FileRules.Where(rule => Regex.IsMatch(file, rule.FileDiscoveryRegEx)))
            {
                if (!rule.Matched)
                {
                    Utils.Trace($"{rule.Name} matched file {file}");
                    rule.Matched = true;
                }

                // if it does then the second regex must be true
                if (Regex.IsMatch(file, rule.FileTestRegEx)) continue;

                Utils.Trace($"File {file} matched by {rule.FileDiscoveryRegEx} but doesn't match {rule.FileTestRegEx}");
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{rule.Name} {rule.Message} {file}");
            }
            if (!mediaBackup.EnsureFile(file)) return Utils.TraceOut(false);

            UpdateMediaFilesCountDisplay();
        }
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Scans all Directories
    /// </summary>
    /// <returns>True if successful otherwise False</returns>
    private bool ScanDirectories()
    {
        Utils.TraceIn();
        var directoriesChecked = new HashSet<string>();
        mediaBackup.ClearFlags();
        Utils.LogWithPushover(BackupAction.ScanDirectory, "Started");
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, string.Empty));

        foreach (var directory in mediaBackup.Config.Directories)
        {
            var scanInfo = new DirectoryScan(directory, DateTime.Now);
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directory));

            if (Directory.Exists(directory))
            {
                if (Utils.IsDirectoryWritable(directory))
                {
                    //We only want to check each root directory once so keep a Hashset of those we've already done
                    var rootPath = Utils.GetRootPath(directory);

                    if (!directoriesChecked.Contains(rootPath))
                    {
                        RootDirectoryChecks(rootPath);
                        directoriesChecked.Add(rootPath);
                    }
                    ScanSingleDirectory(directory, SearchOption.AllDirectories);
                    scanInfo.EndDateTime = DateTime.Now;
                    mediaBackup.DirectoryScans.Add(scanInfo);
                }
                else
                    Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} is not writable");
            }
            else
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{directory} doesn't exist");
        }
        UpdateStatusLabel(Resources.Main_Completed);

        foreach (var rule in mediaBackup.Config.FileRules.Where(static rule => !rule.Matched))
        {
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"{rule.Name} didn't match any files");
        }

        // instead of removing files that are no longer found in Master Directories we now flag them as deleted so we can report them later
        foreach (var backupFile in mediaBackup.BackupFiles.Where(static backupFile => !backupFile.Flag && backupFile.DiskChecked.HasValue()))
        {
            backupFile.Deleted = true;
            backupFile.Flag = true;
        }
        mediaBackup.RemoveFilesWithFlag(false, true);
        mediaBackup.UpdateLastFullScan();
        mediaBackup.Save();
        UpdateStatusLabel(Resources.Main_Saved);
        UpdateMediaFilesCountDisplay();
        var totalFiles = mediaBackup.BackupFiles.Count;
        var totalFileSize = mediaBackup.BackupFiles.Sum(static p => p.Length);
        var filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();
        var notOnBackupDisk = filesNotOnBackupDisk as BackupFile[] ?? filesNotOnBackupDisk.ToArray();
        var fileSizeToCopy = notOnBackupDisk.Sum(static p => p.Length);
        Utils.LogWithPushover(BackupAction.ScanDirectory, $"{totalFiles:n0} files at {Utils.FormatSize(totalFileSize)}");
        var oldestFile = mediaBackup.GetOldestFile();

        if (oldestFile != null)
        {
            var oldestFileDate = DateTime.Parse(oldestFile.DiskChecked);
            var days = DateTime.Today.Subtract(oldestFileDate).Days;
            var daysText = days == 1 ? string.Empty : "s";

            Utils.LogWithPushover(BackupAction.ScanDirectory,
                $"Oldest backup date is {days:n0} day{daysText} ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
        }
        Utils.LogWithPushover(BackupAction.ScanDirectory, $"{notOnBackupDisk.Length:n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");
        Utils.LogWithPushover(BackupAction.ScanDirectory, "Completed");
        return Utils.TraceOut(true);
    }

    private void UpdateOldestBackupDisk()
    {
        var oldestFile = mediaBackup.GetOldestFile();
        if (oldestFile == null) return;

        var days = DateTime.Today.Subtract(DateTime.Parse(oldestFile.DiskChecked)).Days;
        oldestBackupDiskTextBox.Invoke(x => x.Text = oldestFile.Disk);

        oldestBackupDiskAgeTextBox.Invoke(x =>
            x.Text = string.Format(Resources.Main_UpdateOldestBackupDiskNDaysAgo, days, days == 1 ? string.Empty : "s"));
    }

    private void RootDirectoryChecks(string rootDirectory)
    {
        Utils.TraceIn(rootDirectory);
#if !DEBUG
        long readSpeed = 0;
        long writeSpeed = 0;
        var filters = mediaBackup.GetFilters();
        _ = Utils.GetDiskInfo(rootDirectory, out var freeSpaceOnRootDirectoryDisk, out var totalBytesOnRootDirectoryDisk);

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            UpdateStatusLabel(string.Format(Resources.Main_SpeedTesting, rootDirectory));
            Utils.DiskSpeedTest(rootDirectory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations, out readSpeed, out writeSpeed, ct);
        }
        var totalBytesOnRootDirectoryDiskFormatted = Utils.FormatSize(totalBytesOnRootDirectoryDisk);
        var freeSpaceOnRootDirectoryDiskFormatted = Utils.FormatSize(freeSpaceOnRootDirectoryDisk);
        var readSpeedFormatted = Utils.FormatSpeed(readSpeed);
        var writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);
        var text = $"{rootDirectory}\nTotal: {totalBytesOnRootDirectoryDiskFormatted}\nFree: {freeSpaceOnRootDirectoryDiskFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";
        Utils.LogWithPushover(BackupAction.ScanDirectory, text);

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumReadSpeed)) Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumReadSpeed))}");
            if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumWriteSpeed)) Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumWriteSpeed))}");
        }
        if (freeSpaceOnRootDirectoryDisk < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumCriticalSpace)) Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"Free space on {rootDirectory} is too low");
        UpdateStatusLabel(string.Format(Resources.Main_ScanFolders_Deleting_empty_folders_in__0_, rootDirectory));
        var directoriesDeleted = Utils.DeleteEmptyDirectories(rootDirectory);

        foreach (var directory in directoriesDeleted)
        {
            Utils.Log(BackupAction.ScanDirectory, $"Deleted empty folder {directory}");
        }
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, rootDirectory));

        // Check for files in this root directory 
        var files = Utils.GetFiles(rootDirectory, filters, SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            Utils.Trace($"Checking {file}");
            CheckForFilesToDelete(file);
        }
#endif
        Utils.TraceOut();
    }

    [GeneratedRegex(

        // ReSharper disable StringLiteralTypo
        "^(?:.*\\\\_Movies(?:\\s\\(non-tmdb\\))?\\\\(.*)\\s({tmdb-\\d{1,7}?})\\s(?:{edition-(?:(?:[1-7][05]TH\\sANNIVERSARY)|4K|BLURAY|CHRONOLOGICAL|COLLECTORS|" +
        "(?:CRITERION|KL\\sSTUDIO)\\sCOLLECTION|DIAMOND|DVD|IMAX|REDUX|REMASTERED|RESTORED|SPECIAL|(?:" +
        "THE\\sCOMPLETE\\s)?EXTENDED|THE\\sGODFATHER\\sCODA|(?:THE\\sRICHARD\\sDONNER|DIRECTORS|FINAL)\\sCUT|THEATRICAL|ULTIMATE|UNCUT|UNRATED)}\\s)?\\" +
        "[(?:DVD|SDTV|(?:WEB(?:Rip|DL)|Bluray|HDTV|Remux)-(?:48|72|108|216)0p)\\](?:\\[(?:(?:DV)?(?:(?:\\s)?HDR10(?:Plus)?)?|PQ|3D)\\])?" +
        "\\[(?:DTS(?:\\sHD|-(?:X|ES|HD\\s(?:M|HR)A))?|(?:TrueHD|EAC3)(?:\\sAtmos)?|AC3|FLAC|PCM|MP3|A[AV]C|Opus)\\s(?:[1-8]\\.[01])\\]\\" +
        "[(?:[hx]26[45]|MPEG[24]|HEVC|XviD|V(?:C1|P9)|AVC)\\])\\.(m(?:kv|p(?:4|e?g))|ts|avi)$")]

    // ReSharper restore StringLiteralTypo
    private static partial Regex MoviesFilenameRegex();

    private void ScanDirectoryAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        if (!ScanDirectories())
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, Resources.Main_ScanDirectoriesAsync_Scan_Directories_failed);
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    /// <summary>
    ///     Returns True if the file was deleted
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    private bool CheckForFilesToDelete(string filePath)
    {
        var filters = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$");
        var fileName = new FileInfo(filePath).Name;
        if (!filters.Any(pattern => Regex.IsMatch(fileName, pattern))) return false;

        Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, $"File matches RegEx and so will be deleted {filePath}");
        Utils.FileDelete(filePath);
        return true;
    }
}