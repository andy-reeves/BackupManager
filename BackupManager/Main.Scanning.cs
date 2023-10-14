// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.Scanning.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using BackupManager.Entities;
using BackupManager.Extensions;

namespace BackupManager;

internal sealed partial class Main
{
    /// <summary>
    ///     Scan the folder provided.
    /// </summary>
    /// <param name="folderToCheck">The full path to scan</param>
    /// <param name="searchOption">Whether to search subfolders</param>
    /// <returns>True if the scan was successful otherwise False. Returns True if the folder doesn't exist</returns>
    private bool ScanSingleFolder(string folderToCheck, SearchOption searchOption)
    {
        Utils.TraceIn(folderToCheck, searchOption);
        if (!Directory.Exists(folderToCheck)) return Utils.TraceOut(true);

        var subFolderText = searchOption == SearchOption.TopDirectoryOnly ? "folder only" : "and subfolders";
        Utils.LogWithPushover(BackupAction.ScanFolders, $"{folderToCheck}");
        Utils.Trace($"{folderToCheck} {subFolderText}");
        UpdateStatusLabel($"Scanning {folderToCheck}");
        var filters = mediaBackup.GetFilters();
        var files = Utils.GetFiles(folderToCheck, filters, searchOption);
        EnableProgressBar(0, files.Length);

        for (var i = 0; i < files.Length; i++)
        {
            var file = files[i];
            Utils.Trace($"Checking {file}");
            UpdateStatusLabel($"Scanning {folderToCheck}", i + 1);
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
                Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{rule.Name} {rule.Message} {file}");
            }
            if (!mediaBackup.EnsureFile(file)) return Utils.TraceOut(false);

            UpdateMediaFilesCountDisplay();
        }
        return Utils.TraceOut(true);
    }

    /// <summary>
    ///     Scans all MasterFolders and IndexFolders
    /// </summary>
    /// <returns>True if successful otherwise False</returns>
    private bool ScanFolders()
    {
        Utils.TraceIn();
        var filters = mediaBackup.GetFilters();
        long readSpeed = 0, writeSpeed = 0;
        mediaBackup.ClearFlags();
        Utils.LogWithPushover(BackupAction.ScanFolders, "Started");
        UpdateStatusLabel("Started");

        foreach (var masterFolder in mediaBackup.Config.MasterFolders)
        {
            UpdateStatusLabel($"Scanning {masterFolder}");
            if (!Directory.Exists(masterFolder)) Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} is not available");

            if (Directory.Exists(masterFolder))
            {
                if (!Utils.IsDirectoryWritable(masterFolder))
                    Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} is not writable");
                _ = Utils.GetDiskInfo(masterFolder, out var freeSpaceOnCurrentMasterFolder, out var totalBytesOnMasterFolderDisk);

                if (mediaBackup.Config.SpeedTestOnOff)
                {
                    UpdateStatusLabel($"Speed testing {masterFolder}");

                    Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                        out readSpeed, out writeSpeed);
                }
                var totalBytesOnMasterFolderDiskFormatted = Utils.FormatSize(totalBytesOnMasterFolderDisk);
                var freeSpaceOnCurrentMasterFolderFormatted = Utils.FormatSize(freeSpaceOnCurrentMasterFolder);
                var readSpeedFormatted = Utils.FormatSpeed(readSpeed);
                var writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);

                var text =
                    $"{masterFolder}\nTotal: {totalBytesOnMasterFolderDiskFormatted}\nFree: {freeSpaceOnCurrentMasterFolderFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";
                Utils.LogWithPushover(BackupAction.ScanFolders, text);

                if (mediaBackup.Config.SpeedTestOnOff)
                {
                    if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumReadSpeed))
                    {
                        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High,
                            $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumReadSpeed))}");
                    }

                    if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumWriteSpeed))
                    {
                        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High,
                            $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumWriteSpeed))}");
                    }
                }

                if (freeSpaceOnCurrentMasterFolder < Utils.ConvertMBtoBytes(mediaBackup.Config.MasterFolderMinimumCriticalSpace))
                    Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"Free space on {masterFolder} is too low");
                UpdateStatusLabel($"Deleting empty folders in {masterFolder}");
                var directoriesDeleted = Utils.DeleteEmptyDirectories(masterFolder);

                foreach (var directory in directoriesDeleted)
                {
                    Utils.Log(BackupAction.ScanFolders, $"Deleted empty folder {directory}");
                }
                UpdateStatusLabel($"Scanning {masterFolder}");

                // Check for files in the root of the master folder alongside te index folders
                var filesInRootOfMasterFolder = Utils.GetFiles(masterFolder, filters, SearchOption.TopDirectoryOnly);

                foreach (var file in filesInRootOfMasterFolder)
                {
                    Utils.Trace($"Checking {file}");
                    CheckForFilesToDelete(file);
                }

                if (mediaBackup.Config.IndexFolders.Any(indexFolder => !ScanSingleFolder(Path.Combine(masterFolder, indexFolder), SearchOption.AllDirectories)))
                    return false;
            }
            else
                Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{masterFolder} doesn't exist");
        }
        UpdateStatusLabel("Scanning completed.");

        foreach (var rule in mediaBackup.Config.FileRules.Where(static rule => !rule.Matched))
        {
            Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{rule.Name} didn't match any files");
        }

        // instead of removing files that are no longer found in Master Folders we now flag them as deleted so we can report them later
        foreach (var backupFile in mediaBackup.BackupFiles.Where(static backupFile => !backupFile.Flag && backupFile.DiskChecked.HasValue()))
        {
            backupFile.Deleted = true;
            backupFile.Flag = true;
        }
        mediaBackup.RemoveFilesWithFlag(false, true);
        mediaBackup.UpdateLastFullScan();
        mediaBackup.Save();
        UpdateStatusLabel("Saved.");
        UpdateMediaFilesCountDisplay();
        var totalFiles = mediaBackup.BackupFiles.Count;
        var totalFileSize = mediaBackup.BackupFiles.Sum(static p => p.Length);
        var filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();
        var notOnBackupDisk = filesNotOnBackupDisk as BackupFile[] ?? filesNotOnBackupDisk.ToArray();
        var fileSizeToCopy = notOnBackupDisk.Sum(static p => p.Length);
        Utils.LogWithPushover(BackupAction.ScanFolders, $"{totalFiles:n0} files at {Utils.FormatSize(totalFileSize)}");
        var oldestFile = mediaBackup.GetOldestFile();

        if (oldestFile != null)
        {
            var oldestFileDate = DateTime.Parse(oldestFile.DiskChecked);
            var days = DateTime.Today.Subtract(oldestFileDate).Days;
            var daysText = days == 1 ? string.Empty : "(s)";

            Utils.LogWithPushover(BackupAction.ScanFolders,
                $"Oldest backup date is {days:n0} day{daysText} ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
        }
        Utils.LogWithPushover(BackupAction.ScanFolders, $"{notOnBackupDisk.Length:n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");
        Utils.LogWithPushover(BackupAction.ScanFolders, "Completed");
        return Utils.TraceOut(true);
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

    private void ScanFolderAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        _ = ScanFolders();
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

        Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.Normal, $"File matches RegEx and so will be deleted {filePath}");
        Utils.FileDelete(filePath);
        return true;
    }
}