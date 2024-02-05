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
    /// <param name="scanPathForVideoCodec"></param>
    /// <param name="ct"></param>
    /// <returns>True if the scan was successful otherwise False. Returns True if the directories doesn't exist</returns>
    private bool ScanSingleDirectory(string directoryToCheck, SearchOption searchOption, bool scanPathForVideoCodec, CancellationToken ct)
    {
        Utils.TraceIn(directoryToCheck, searchOption);
        if (!Directory.Exists(directoryToCheck)) return Utils.TraceOut(true);

        Utils.LogWithPushover(BackupAction.ScanDirectory, $"{directoryToCheck}", false, true);
        UpdateStatusLabel(ct, string.Format(Resources.Scanning, directoryToCheck));
        var files = Utils.GetFiles(directoryToCheck, mediaBackup.GetFilters(), searchOption, ct);
        var subDirectoryText = searchOption == SearchOption.TopDirectoryOnly ? "directories only" : "and subdirectories";
        Utils.Trace($"{directoryToCheck} {subDirectoryText}");
        var scanId = Guid.NewGuid().ToString();
        return ProcessFiles(files, scanId, scanPathForVideoCodec, ct);
    }

    /// <summary>
    ///     Processes the files provided on multiple threads (1 per disk)
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="scanPathForVideoCodec"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private bool ProcessFiles(IReadOnlyCollection<string> filesParam, string scanId, bool scanPathForVideoCodec, CancellationToken ct)
    {
        Utils.TraceIn();
        DisableControlsForAsyncTasks(ct);
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
        var tasks = new List<Task<bool>>(diskNames.Length);
        fileCounterForMultiThreadProcessing = 0;
        EnableProgressBar(0, filesParam.Count);
        var suffix = filesParam.Count == 1 ? string.Empty : "s";
        Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.Normal, $"Processing {filesParam.Count:n0} file{suffix}", false, true);

        // One process thread for each disk
        tasks.AddRange(diskNames.Select(diskName => Utils.GetFilesForDisk(diskName, filesParam)).Select(files =>
            TaskWrapper(Task.Run(() => ProcessFilesInternal(files, scanId, scanPathForVideoCodec, ct), ct), ct)));

        // this is to have only 1 thread processing files
        // tasks.Add(TaskWrapper(Task.Run(() => ProcessFilesInternal(filesParam.ToArray(), scanId, scanPathForVideoCodec, ct), ct), ct));
        Task.WhenAll(tasks).Wait(ct);
        var returnValue = !tasks.Any(static t => !t.Result);
        if (returnValue) Utils.LogWithPushover(BackupAction.ProcessFiles, Resources.Completed, true, true);
        UpdateMediaFilesCountDisplay();
        ResetAllControls();
        return Utils.TraceOut(returnValue);
    }

    /// <summary>
    ///     Internal function to process the files provided
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="scanPathForVideoCodec"></param>
    /// <param name="ct"></param>
    private bool ProcessFilesInternal(IEnumerable<string> filesParam, string scanId, bool scanPathForVideoCodec, CancellationToken ct)
    {
        Utils.TraceIn();

        // we need a blocking collection and then copy it back when it's all done
        // then split by disk name and have a Task for each of them like the directory scanner

        var filtersToDelete = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$").ToArray();

        // order the files by path so that we can track when the monitored directories are changing for scan timings
        var files = filesParam.OrderBy(static f => f.ToString()).ToList();
        var directoryScanning = string.Empty;
        var firstDir = true;
        DirectoryScan scanInfo = null;

        foreach (var fileInsideForEach in files)
        {
            var file = fileInsideForEach;

            lock (_lock)
            {
                if (file.Length > Utils.MAX_PATH)
                {
                    Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.PathTooLong, file));
                    return Utils.TraceOut(false);
                }

                if (Utils.StringContainsFixedSpace(file))
                {
                    Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.PathHasAFixedSpace, file));

                    try
                    {
                        file = Utils.RenameFileToRemoveFixedSpaces(file);
                    }
                    catch (Exception ex)
                    {
                        if (ex is not (IOException or NotSupportedException)) throw;

                        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.FileIsLocked, file));
                        return Utils.TraceOut(false);
                    }
                }

                if (config.DirectoriesRenameVideoFilesOnOff && scanPathForVideoCodec && File.Exists(file) && Utils.FileIsVideo(file))
                {
                    try
                    {
                        if (Utils.RenameVideoCodec(file, out var newFile))
                        {
                            Utils.LogWithPushover(BackupAction.ProcessFiles,
                                string.Format(Resources.FileRenameRequiredForVideoCodec, Path.GetFileName(file)));

                            // change the file to the newFile to continue processing
                            file = newFile;
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex is not (IOException or NotSupportedException)) throw;

                        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.FileIsLocked, file));
                        return Utils.TraceOut(false);
                    }
                }
                if (file == null) return Utils.TraceOut(false);

                fileCounterForMultiThreadProcessing++;
                if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                currentPercentComplete = fileCounterForMultiThreadProcessing * 100 / toolStripProgressBar.Maximum;

                if (currentPercentComplete % 25 == 0 && currentPercentComplete > reportedPercentComplete && files.Count > 100)
                {
                    reportedPercentComplete = currentPercentComplete;
                    Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.ProcessingPercentage, currentPercentComplete));
                }
                Utils.Trace($"{fileCounterForMultiThreadProcessing} Processing {file}");
            }
            _ = mediaBackup.GetFoldersForPath(file, out var directory, out _);

            if (directory != directoryScanning)
            {
                if (!firstDir) scanInfo.EndDateTime = DateTime.Now;

                scanInfo = new DirectoryScan(DirectoryScanType.ProcessingFiles, directory, DateTime.Now, scanId)
                {
                    TotalFiles = files.Count(f => f.StartsWithIgnoreCase(directory))
                };
                mediaBackup.DirectoryScans.Add(scanInfo);
                directoryScanning = directory;
                firstDir = false;
            }
            UpdateStatusLabel(ct, Resources.Processing, fileCounterForMultiThreadProcessing);
            if (CheckForFilesToDelete(file, filtersToDelete)) continue;

            // RegEx file name rules
            ProcessFileRules(file);

            try
            {
                if (!mediaBackup.EnsureFile(file)) return Utils.TraceOut(false);
            }
            catch (IOException)
            {
                // exception accessing the file return false and we will try again 
                return Utils.TraceOut(false);
            }
        }

        // Update the last scan endDateTime as it wasn't set in the loop
        if (scanInfo != null) scanInfo.EndDateTime = DateTime.Now;
        return Utils.TraceOut(true);
    }

    private void ProcessFileRules(string file)
    {
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
    }

    private void UpdateOldestBackupDisk()
    {
        Utils.TraceIn();
        var oldestFile = mediaBackup.GetOldestFile();
        if (oldestFile == null) return;

        oldestBackupDiskTextBox.TextWithInvoke(oldestFile.Disk);
        var days = DateTime.Today.Subtract(DateTime.Parse(oldestFile.DiskChecked)).Days;
        oldestBackupDiskAgeTextBox.TextWithInvoke(string.Format(Resources.UpdateOldestBackupDiskNDaysAgo, days, days == 1 ? string.Empty : "s"));
        Utils.TraceOut();
    }

    private void RootDirectoryChecks(string rootDirectory, CancellationToken ct)
    {
        Utils.TraceIn(rootDirectory);
        long readSpeed = 0;
        long writeSpeed = 0;
        var filters = mediaBackup.GetFilters();
        _ = Utils.GetDiskInfo(rootDirectory, out var freeSpaceOnRootDirectoryDisk, out var totalBytesOnRootDirectoryDisk);

        if (mediaBackup.Config.SpeedTestOnOff)
        {
            UpdateStatusLabel(ct, string.Format(Resources.SpeedTesting, rootDirectory));

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
        UpdateStatusLabel(ct, string.Format(Resources.Scanning, rootDirectory));

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

    private void ScanAllDirectoriesAsync(CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.Started, false, true);
            UpdateStatusLabel(ct, string.Format(Resources.Scanning, string.Empty));
            ScanAllDirectories(false, ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ScanAllDirectories(bool updateLastFullScan, CancellationToken ct)
    {
        var scanId = Guid.NewGuid().ToString();
        fileBlockingCollection = new BlockingCollection<string>();
        directoryScanBlockingCollection = new BlockingCollection<DirectoryScan>();

        // split the directories into group by the disk name
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.Directories);
        RootDirectoryChecks(mediaBackup.Config.Directories, ct);
        var tasks = new List<Task>(diskNames.Length);

        tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.Directories))
            .Select(directoriesOnDisk => { return TaskWrapper(Task.Run(() => GetFilesAsync(directoriesOnDisk, scanId, ct), ct), ct); }));
        Task.WhenAll(tasks).Wait(ct);
        Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.Completed, true);

        foreach (var scan in directoryScanBlockingCollection)
        {
            mediaBackup.DirectoryScans.Add(scan);
        }

        // Save now in case the scanning files is interrupted
        mediaBackup.Save(ct);
        mediaBackup.ClearFlags();

        if (!ProcessFiles(fileBlockingCollection, scanId, config.DirectoriesRenameVideoFilesForFullScansOnOff, ct))
            Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.ScanDirectoriesFailed);
        var filesToRemoveOrMarkDeleted = mediaBackup.BackupFiles.Where(static b => !b.Flag).ToArray();
        RemoveOrDeleteFiles(filesToRemoveOrMarkDeleted, out _, out _);
        if (updateLastFullScan) mediaBackup.UpdateLastFullScan();
        mediaBackup.Save(ct);
        UpdateMediaFilesCountDisplay();
    }

    private void GetFilesAsync(IEnumerable<string> directories, string scanId, CancellationToken ct)
    {
        Utils.TraceIn();
        var filters = mediaBackup.GetFilters();

        foreach (var directory in directories)
        {
            var directoryScan = new DirectoryScan(DirectoryScanType.GetFiles, directory, DateTime.Now, scanId);
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, string.Format(Resources.Scanning, directory));
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
            UpdateStatusLabel(ct, string.Format(Resources.Scanning, directory));
            var files = Utils.GetFiles(directory, filters, SearchOption.AllDirectories, ct);
            directoryScan.EndDateTime = DateTime.Now;

            foreach (var file in files)
            {
                fileBlockingCollection.Add(file, ct);
            }
            directoryScan.TotalFiles = files.Length;
            directoryScanBlockingCollection.Add(directoryScan, ct);
        }
        Utils.TraceOut();
    }

    private void ScanDirectoryAsync(string directory, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);

            ReadyToScan(new FileSystemWatcherEventArgs(directory), SearchOption.AllDirectories,
                config.DirectoriesRenameVideoFilesForFullScansOnOff, ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    private void ProcessFilesAsync(CancellationToken token)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(token);
            var files = mediaBackup.BackupFiles.Where(static file => !file.Deleted).Select(static file => file.FullPath).ToList();
            var scanId = Guid.NewGuid().ToString();
            mediaBackup.ClearFlags();
            if (ProcessFiles(files, scanId, config.DirectoriesRenameVideoFilesForFullScansOnOff, token)) mediaBackup.Save(token);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
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