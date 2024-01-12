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
        var scanId = Guid.NewGuid().ToString();
        return ProcessFiles(files, scanId, ct);
    }

    /// <summary>
    ///     Processes the files provided on multiple threads (1 per disk)
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    private bool ProcessFiles(IReadOnlyCollection<string> filesParam, string scanId, CancellationToken token)
    {
        Utils.TraceIn();
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        try
        {
            var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
            var tasks = new List<Task<bool>>(diskNames.Length);
            fileCounterForMultiThreadProcessing = 0;
            EnableProgressBar(0, filesParam.Count);
            var suffix = filesParam.Count == 1 ? string.Empty : "s";

            Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.Normal,
                $"Processing {filesParam.Count:n0} file{suffix}");

            // One process thread for each disk or just one for all
#if !DEBUG
            tasks.AddRange(diskNames.Select(diskName => Utils.GetFilesForDisk(diskName, filesParam)).Select(files => TaskWrapper(ProcessFilesA, files, scanId, token)));
#else
            tasks.Add(TaskWrapper(ProcessFilesA, filesParam.ToArray(), scanId, token));
#endif
            Task.WhenAll(tasks).Wait(ct);
            var returnValue = !tasks.Any(static t => !t.Result);
            if (returnValue) Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.Normal, Resources.Main_Completed);
            UpdateMediaFilesCountDisplay();
            ResetAllControls();
            longRunningActionExecutingRightNow = false;
            return Utils.TraceOut(returnValue);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace("Cancelling ProcessFiles");
            ASyncTasksCleanUp();
            return Utils.TraceOut(false);
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
        return Utils.TraceOut(false);
    }

    /// <summary>
    ///     Internal function to process the files provided
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="token"></param>
    private bool ProcessFilesA(string[] filesParam, string scanId, CancellationToken token)
    {
        Utils.TraceIn();

        // we need a blocking collection and then copy it back when its all done
        // then split by disk name and have a Task for each of them like the directory scanner

        var filtersToDelete = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$").ToArray();

        // order the files by path so we can track when the monitored directories are changing for scan timings
        var files = filesParam.OrderBy(static f => f.ToString()).ToList();
        var directoryScanning = string.Empty;
        var firstDir = true;
        DirectoryScan scanInfo = null;

        foreach (var fileInsideForEach in files)
        {
            var file = fileInsideForEach;

            lock (_lock)
            {
                if (Utils.StringContainsFixedSpace(file))
                {
                    Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High,
                        $"{file} has a fixed space so renaming it");

                    if (Utils.IsFileAccessible(file))
                        file = Utils.RenameFileToRemoveFixedSpaces(file);
                    else
                    {
                        Utils.LogWithPushover(BackupAction.ProcessFiles, $"{file} is locked so can't be renamed now.");
                        return Utils.TraceOut(false);
                    }
                }
                fileCounterForMultiThreadProcessing++;
                if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();
                currentPercentComplete = fileCounterForMultiThreadProcessing * 100 / toolStripProgressBar.Maximum;

                if (currentPercentComplete % 25 == 0 && currentPercentComplete > reportedPercentComplete && files.Count > 100)
                {
                    reportedPercentComplete = currentPercentComplete;

                    Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.Normal,
                        $"Processing {currentPercentComplete}%");
                }
                Utils.Trace($"{fileCounterForMultiThreadProcessing} Processing {file}");
            }
            _ = mediaBackup.GetFoldersForPath(file, out var directory, out _);

            if (directory != directoryScanning)
            {
                if (!firstDir) scanInfo.EndDateTime = DateTime.Now;
                scanInfo = new DirectoryScan(DirectoryScanType.ProcessingFiles, directory, DateTime.Now, scanId);
                mediaBackup.DirectoryScans.Add(scanInfo);
                directoryScanning = directory;
                firstDir = false;
            }
            UpdateStatusLabel("Processing", fileCounterForMultiThreadProcessing);
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
                Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High, $"{rule.Name} {rule.Message} {file}");

                // If a rule has failed then break to avoid multiple messages sent
                break;
            }
            if (!mediaBackup.EnsureFile(file)) return Utils.TraceOut(false);
        }

        // Update the last scan endDateTime as it wasn't set in the loop
        if (scanInfo != null) scanInfo.EndDateTime = DateTime.Now;
        return Utils.TraceOut(true);
    }

    private void UpdateOldestBackupDisk()
    {
        var oldestFile = mediaBackup.GetOldestFile();
        if (oldestFile == null) return;

        oldestBackupDiskTextBox.TextWithInvoke(oldestFile.Disk);
        var days = DateTime.Today.Subtract(DateTime.Parse(oldestFile.DiskChecked)).Days;

        oldestBackupDiskAgeTextBox.TextWithInvoke(string.Format(Resources.Main_UpdateOldestBackupDiskNDaysAgo, days,
            days == 1 ? string.Empty : "s"));
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

            Utils.DiskSpeedTest(rootDirectory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize),
                mediaBackup.Config.SpeedTestIterations, out readSpeed, out writeSpeed, ct);
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
        Utils.TraceIn();

        if (longRunningActionExecutingRightNow)
        {
            Utils.TraceOut();
            return;
        }
        longRunningActionExecutingRightNow = true;
        DisableControlsForAsyncTasks();

        try
        {
            Utils.LogWithPushover(BackupAction.ScanDirectory, "Started");
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, string.Empty));
            ScanAllDirectories(false);
            ResetAllControls();
            longRunningActionExecutingRightNow = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Utils.Trace("Cancelling ScanAllDirectoriesAsync");
            ASyncTasksCleanUp();
        }
        catch (Exception u)
        {
            Utils.LogWithPushover(BackupAction.Error, PushoverPriority.High,
                string.Format(Resources.Main_TaskWrapperException, u));
        }
    }

    private void ScanAllDirectories(bool updateLastFullScan)
    {
        var scanId = Guid.NewGuid().ToString();
        ResetTokenSource();
        fileBlockingCollection = new BlockingCollection<string>();
        directoryScanBlockingCollection = new BlockingCollection<DirectoryScan>();

        // split the directories into group by the disk name
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
        RootDirectoryChecks(mediaBackup.Config.Directories);
        var tasks = new List<Task>(diskNames.Length);

        tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.Directories))
            .Select(directoriesOnDisk => TaskWrapper(GetFilesAsync, directoriesOnDisk, scanId)));
        Task.WhenAll(tasks).Wait(ct);
        Utils.LogWithPushover(BackupAction.ScanDirectory, "Scanning complete.");

        foreach (var scan in directoryScanBlockingCollection)
        {
            mediaBackup.DirectoryScans.Add(scan);
        }

        // Save now in case the scanning files is interrupted
        mediaBackup.Save();
        mediaBackup.ClearFlags();

        if (!ProcessFiles(fileBlockingCollection, scanId, ct))
        {
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal,
                Resources.Main_ScanDirectoriesAsync_Scan_Directories_failed);
        }
        var filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).ToArray();
        RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted, out _, out _);
        if (updateLastFullScan) mediaBackup.UpdateLastFullScan();
        mediaBackup.Save();
        UpdateMediaFilesCountDisplay();
    }

    private void GetFilesAsync(string[] directories, string scanId)
    {
        var filters = mediaBackup.GetFilters();

        foreach (var directory in directories)
        {
            var directoryScan = new DirectoryScan(DirectoryScanType.GetFiles, directory, DateTime.Now, scanId);

            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal,
                string.Format(Resources.Main_Scanning, directory));
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            UpdateStatusLabel(string.Format(Resources.Main_Scanning, directory));
            var files = Utils.GetFiles(directory, filters, SearchOption.AllDirectories, ct);
            directoryScan.EndDateTime = DateTime.Now;

            foreach (var file in files)
            {
                fileBlockingCollection.Add(file, ct);
            }
            directoryScanBlockingCollection.Add(directoryScan, ct);
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

    private void ProcessFilesAsync()
    {
        var files = mediaBackup.BackupFiles.Where(static file => !file.Deleted).Select(static file => file.FullPath).ToList();
        var scanId = Guid.NewGuid().ToString();
        mediaBackup.ClearFlags();
        _ = ProcessFiles(files, scanId, ct);
        mediaBackup.Save();
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

        Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal,
            $"File matches RegEx and so will be deleted {filePath}");
        Utils.FileDelete(filePath);
        return true;
    }
}