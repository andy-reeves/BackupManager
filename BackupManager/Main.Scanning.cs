// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.Scanning.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using BackupManager.Entities;
using BackupManager.Extensions;
using BackupManager.Properties;

namespace BackupManager;

internal sealed partial class Main
{
    /// <summary>
    ///     Scan the directories provided.
    /// </summary>
    /// <param name="directoryToCheck">The full path to scan</param>
    /// <param name="searchOption">Whether to search subdirectories</param>
    /// <returns>True if the scan was successful otherwise False. Returns True if the directories doesn't exist</returns>
    private bool ScanSingleDirectory(string directoryToCheck, SearchOption searchOption)
    {
        Utils.TraceIn(directoryToCheck, searchOption);
        if (!Directory.Exists(directoryToCheck)) return Utils.TraceOut(true);

        Utils.LogWithPushover(BackupAction.ScanDirectory, $"{directoryToCheck}");
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck));
        var files = Utils.GetFiles(directoryToCheck, mediaBackup.GetFilters(), searchOption, ct);
        var subDirectoryText = searchOption == SearchOption.TopDirectoryOnly ? "directories only" : "and subdirectories";
        Utils.Trace($"{directoryToCheck} {subDirectoryText}");
        return ScanFiles(files, ct);
    }

    private bool ScanFiles(IReadOnlyCollection<string> filesParam, CancellationToken token)
    {
        var filtersToDelete = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$").ToArray();
        EnableProgressBar(0, filesParam.Count);
        Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, "Processing scanned files");
        var reportedPercentComplete = 0;

        // order the files by path so we can track when the monitored directories are changing for scan timings
        var files = filesParam.OrderBy(static f => f.ToString()).ToList();
        var directoryScanning = string.Empty;
        var firstDir = true;
        DirectoryScan scanInfo = null;

        for (var i = 0; i < files.Count; i++)
        {
            if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
            var currentPercentComplete = i * 100 / files.Count;

            if (currentPercentComplete % 10 == 0 && currentPercentComplete > reportedPercentComplete)
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, $"Scanning {currentPercentComplete}%");
                reportedPercentComplete = currentPercentComplete;
            }
            var file = files[i];
            _ = mediaBackup.GetFoldersForPath(file, out var directory, out _);

            if (directory != directoryScanning)
            {
                if (!firstDir) scanInfo.EndDateTime = DateTime.Now;
                scanInfo = new DirectoryScan(DirectoryScanType.ProcessingFiles, directory, DateTime.Now);
                mediaBackup.DirectoryScans.Add(scanInfo);
                directoryScanning = directory;
                firstDir = false;
            }
            Utils.Trace($"Checking {file}");
            var directoryToCheck = new FileInfo(file).DirectoryName;
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directoryToCheck), i + 1);
            if (CheckForFilesToDelete(file, filtersToDelete)) continue;

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
        }
        UpdateMediaFilesCountDisplay();
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
        long readSpeed = 0;
        long writeSpeed = 0;
        var filters = mediaBackup.GetFilters();
        _ = Utils.GetDiskInfo(rootDirectory, out var freeSpaceOnRootDirectoryDisk, out var totalBytesOnRootDirectoryDisk);

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            UpdateStatusLabel(string.Format(Resources.Main_SpeedTesting, rootDirectory));

            Utils.DiskSpeedTest(rootDirectory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations,
                out readSpeed, out writeSpeed, ct);
        }
        var totalBytesOnRootDirectoryDiskFormatted = Utils.FormatSize(totalBytesOnRootDirectoryDisk);
        var freeSpaceOnRootDirectoryDiskFormatted = Utils.FormatSize(freeSpaceOnRootDirectoryDisk);
        var readSpeedFormatted = Utils.FormatSpeed(readSpeed);
        var writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);

        var text =
            $"{rootDirectory}\nTotal: {totalBytesOnRootDirectoryDiskFormatted}\nFree: {freeSpaceOnRootDirectoryDiskFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";
        Utils.LogWithPushover(BackupAction.ScanDirectory, text);

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumReadSpeed))
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High,
                    $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumReadSpeed))}");
            }

            if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumWriteSpeed))
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High,
                    $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumWriteSpeed))}");
            }
        }

        if (freeSpaceOnRootDirectoryDisk < Utils.ConvertMBtoBytes(mediaBackup.Config.DirectoriesMinimumCriticalSpace))
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"Free space on {rootDirectory} is too low");
        UpdateStatusLabel(string.Format(Resources.Main_Scanning, rootDirectory));

        // Check for files in this root directories 
        var files = Utils.GetFiles(rootDirectory, filters, SearchOption.TopDirectoryOnly, ct);

        var filtersToDelete = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$").ToArray();

        foreach (var file in files)
        {
            Utils.Trace($"Checking {file}");
            _ = CheckForFilesToDelete(file, filtersToDelete);
        }
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

    private void ScanAllDirectoriesAsync()
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        try
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Utils.LogWithPushover(BackupAction.ScanDirectory, "Started");
            fileBlockingCollection = new BlockingCollection<string>();

            // split the directories into group by the disk name
            var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
            RootDirectoryChecks(mediaBackup.Config.Directories);
            var tasks = new List<Task>(diskNames.Length);

            tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.Directories))
                .Select(directoriesOnDisk => TaskWrapper(GetFilesAsync, directoriesOnDisk)));
            Task.WhenAll(tasks).Wait(ct);

            if (!ScanFiles(fileBlockingCollection, ct))
            {
                Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal,
                    Resources.Main_ScanDirectoriesAsync_Scan_Directories_failed);
            }
            mediaBackup.Save();
            ResetAllControls();
            longRunningActionExecutingRightNow = false;
        }
        catch (Exception u)
        {
            Utils.Trace("Exception in the TaskWrapper");

            if (u.Message != "The operation was canceled.")
                Utils.LogWithPushover(BackupAction.General, PushoverPriority.High, string.Format(Resources.Main_TaskWrapperException, u));
            ASyncTasksCleanUp();
        }
    }

    private void GetFilesAsync(string[] directories)
    {
        var filters = mediaBackup.GetFilters();

        foreach (var directory in directories)
        {
            var directoryScan = new DirectoryScan(DirectoryScanType.GetFiles, directory, DateTime.Now);
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, string.Format(Resources.Main_Scanning, directory));
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directory));
            var files = Utils.GetFiles(directory, filters, SearchOption.AllDirectories, ct);
            directoryScan.EndDateTime = DateTime.Now;
            mediaBackup.DirectoryScans.Add(directoryScan);

            foreach (var file in files)
            {
                fileBlockingCollection.Add(file, ct);
            }
        }
    }

    private void ScanDirectoryAsync(string directory)
    {
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();
        ReadyToScan(new FileSystemWatcherEventArgs(directory), SearchOption.AllDirectories);
        ResetAllControls();
        longRunningActionExecutingRightNow = false;
    }

    /// <summary>
    ///     Returns True if the file was deleted
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="filters">The filters to find files to delete</param>
    /// <returns></returns>
    private static bool CheckForFilesToDelete(string filePath, IEnumerable<string> filters)
    {
        var fileName = new FileInfo(filePath).Name;
        if (!filters.Any(pattern => Regex.IsMatch(fileName, pattern))) return false;

        Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, $"File matches RegEx and so will be deleted {filePath}");
        Utils.FileDelete(filePath);
        return true;
    }
}