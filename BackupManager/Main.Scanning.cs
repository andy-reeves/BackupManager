﻿// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.Scanning.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
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
        var files = Utils.File.GetFiles(directoryToCheck, mediaBackup.GetFilters(), searchOption, ct);
        var subDirectoryText = searchOption == SearchOption.TopDirectoryOnly ? "directories only" : "and subdirectories";
        Utils.Trace($"{directoryToCheck} {subDirectoryText}");
        var scanId = Guid.NewGuid().ToString();
        return ProcessFiles(files, scanId, scanPathForVideoCodec, true, ct);
    }

    /// <summary>
    ///     Processes the files provided on multiple threads (1 per disk)
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="scanPathForVideoCodec"></param>
    /// <param name="autoScan"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private bool ProcessFiles(IReadOnlyCollection<string> filesParam, string scanId, bool scanPathForVideoCodec, bool autoScan, CancellationToken ct)
    {
        Utils.TraceIn();
        DisableControlsForAsyncTasks(ct);
        var disksAndFirstDirectories = Utils.GetDiskAndFirstDirectory(mediaBackup.Config.DirectoriesToBackup);
        var tasks = new List<Task<bool>>(disksAndFirstDirectories.Length);
        fileCounterForMultiThreadProcessing = 0;
        reportedPercentComplete = 0;
        EnableProgressBar(0, filesParam.Count);
        var suffix = filesParam.Count == 1 ? string.Empty : "s";
        Utils.LogWithPushover(BackupAction.ProcessFiles, $"Processing {filesParam.Count:n0} file{suffix}", false, true);
        mediaBackup.LastDuplicateDirectory = string.Empty;

        // One process thread for each disk that has files on it to scan
        tasks.AddRange(disksAndFirstDirectories.Select(diskName => Utils.GetFilesForDisk(diskName, filesParam)).Select(files =>
            TaskWrapper(Task.Run(() => files == null || files.Length == 0 || ProcessFilesInternal(files, scanId, scanPathForVideoCodec, autoScan, ct), ct), ct)));

        // this is to have only 1 thread processing files
        // tasks.Add(TaskWrapper(Task.Run(() => ProcessFilesInternal(filesParam.ToArray(), scanId, scanPathForVideoCodec, ct), ct), ct));
        Task.WhenAll(tasks).Wait(ct);
        var returnValue = !tasks.Any(static t => !t.Result);
        if (returnValue) Utils.LogWithPushover(BackupAction.ProcessFiles, Resources.Completed, true, true);
        UpdateMediaFilesCountDisplay();

        // Do NOT call ResetAllControls here because we are often part of a much longer task
        return Utils.TraceOut(returnValue);
    }

    /// <summary>
    ///     Internal function to process the files provided
    /// </summary>
    /// <param name="filesParam"></param>
    /// <param name="scanId"></param>
    /// <param name="scanPathForVideoCodec"></param>
    /// <param name="autoScan"></param>
    /// <param name="ct"></param>

    // ReSharper disable once FunctionComplexityOverflow
    private bool ProcessFilesInternal(IEnumerable<string> filesParam, string scanId, bool scanPathForVideoCodec, bool autoScan, CancellationToken ct)
    {
        Utils.TraceIn();

        // we need a blocking collection and then copy it back when it's all done
        // then split by disk name and have a Task for each of them like the directory scanner
        var filtersToDelete = FiltersToDelete();

        // order the files by path so that we can track when the monitored directories are changing for scan timings
        var files = filesParam.OrderBy(static f => f.ToString()).ToArray();
        var directoryScanning = string.Empty;
        var firstDir = true;
        DirectoryScan scanInfo = null;

        foreach (var fileInsideForEach in files)
        {
            var file = fileInsideForEach;

            try
            {
                if (!ProcessFilesInternalFinal(scanPathForVideoCodec, ref file, autoScan, ct)) continue;

                directoryScanning = DirectoryScanning(scanId, file, directoryScanning, files, ref firstDir, ref scanInfo);
                UpdateStatusLabel(ct, string.Format(Resources.Processing, Path.GetDirectoryName(file)), fileCounterForMultiThreadProcessing);
                if (CheckForFilesToDelete(file, filtersToDelete)) continue;

                // Only process the naming rules after we've ensured the file is in our xml file
                if (mediaBackup.EnsureFile(file)) ProcessFileRules(file);
            }
            catch (IOException)
            {
                // exception accessing the file so report it and skip this file for now
                Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.High, $"Unable to calculate the hash code for {file}.");
            }
        }

        // Update the last scan endDateTime as it wasn't set in the loop
        if (scanInfo != null) scanInfo.EndDateTime = DateTime.Now;
        return Utils.TraceOut(true);
    }

    private bool ProcessFilesInternalFinal(bool scanPathForVideoCodec, ref string file, bool autoScan, CancellationToken ct)
    {
        if (!ProcessFilesMaxPathCheck(file)) return false;
        if (!ProcessFilesFixedSpaceCheck(ref file)) return false;
        if (!ProcessFilesRenameFileRules(ref file)) return false;
        if (!ProcessFilesCheckAllMediaInfo(scanPathForVideoCodec, ref file, ct)) return false;

        ct.ThrowIfCancellationRequested();
        if (file == null) return false;

        var runtimeFromCache = mediaBackup.GetVideoRuntime(file);

        if (runtimeFromCache > -1)
        {
            // Check the runtime of the video file
            Utils.MediaHelper.CheckRuntimeForMovieOrTvEpisode(file, runtimeFromCache, config.VideoMinimumPercentageDifferenceForRuntime,
                config.VideoMaximumPercentageDifferenceForRuntime, autoScan);
        }
        if (!mediaBackup.CheckTvEpisodeForDuplicate(file, ct)) return false;

        ProcessFilesUpdatePercentComplete(file);
        return true;
    }

    private string DirectoryScanning(string scanId, string file, string directoryScanning, IEnumerable<string> files, ref bool firstDir, ref DirectoryScan scanInfo)
    {
        _ = mediaBackup.GetFoldersForPath(file, out var directory, out _);
        if (directory == directoryScanning) return directoryScanning;

        if (!firstDir) scanInfo.EndDateTime = DateTime.Now;

        scanInfo = new DirectoryScan(DirectoryScanType.ProcessingFiles, directory, DateTime.Now, scanId)
        {
            TotalFiles = files.Count(f => f.StartsWithIgnoreCase(Utils.EnsurePathHasATerminatingSeparator(directory)))
        };
        mediaBackup.DirectoryScans.Add(scanInfo);
        directoryScanning = directory;
        firstDir = false;
        return directoryScanning;
    }

    private string[] FiltersToDelete()
    {
        return Enumerable.ToArray(mediaBackup.Config.FilesToDelete
            .Select(static filter => filter.StartsWithIgnoreCase("^") ? filter : $"^{filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".")}$").ToList());
    }

    /// <summary>
    ///     Returns false if a rename was required but failed
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private bool ProcessFilesRenameFileRules(ref string file)
    {
        Utils.TraceIn();
        var newFilePath = file;

        foreach (var fileRenameRule in config.FileRenameRules.Select(static fileRenameRule => new { fileRenameRule, a = fileRenameRule.FileDiscoveryRegex })
                     .Where(static t => !t.a.HasNoValue()).Select(t => new { t, match = Regex.Match(newFilePath, t.a) }).Where(static t => t.match.Success)
                     .Select(static t => t.t.fileRenameRule))
        {
            // split on the comma, trim them, then replace the text
            newFilePath = fileRenameRule.Search.Split(',').Select(static t => t.Trim()).Aggregate(newFilePath, (current, s) => current.Replace(s, fileRenameRule.Replace));
        }
        if (newFilePath == file) return Utils.TraceOut(true);

        Utils.LogWithPushover(BackupAction.ProcessFiles, $"{file} being renamed to {newFilePath}");
        if (!Utils.File.Move(file, newFilePath)) return Utils.TraceOut(false);

        file = newFilePath;
        return Utils.TraceOut(true);
    }

    private void ProcessFilesUpdatePercentComplete(string file)
    {
        Utils.TraceIn(file);
        fileCounterForMultiThreadProcessing++;
        currentPercentComplete = fileCounterForMultiThreadProcessing * 100 / toolStripProgressBar.Maximum;
        Utils.Trace($"fileCounterForMultiThreadProcessing  = {fileCounterForMultiThreadProcessing}");
        Utils.Trace($"currentPercentComplete  = {currentPercentComplete}");
        Utils.Trace($"toolStripProgressBar.Maximum  = {toolStripProgressBar.Maximum}");
        Utils.Trace($"reportedPercentComplete  = {reportedPercentComplete}");
        if (currentPercentComplete % 10 != 0 || currentPercentComplete <= reportedPercentComplete || toolStripProgressBar.Maximum < 25) return;

        reportedPercentComplete = currentPercentComplete;
        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.ProcessingPercentage, currentPercentComplete), true, true);
        Utils.Trace($"{fileCounterForMultiThreadProcessing} Processing {file}");
        Utils.TraceOut();
    }

    private bool ProcessFilesCheckAllMediaInfo(bool scan, ref string file, CancellationToken ct)
    {
        Utils.TraceIn();
        ArgumentException.ThrowIfNullOrEmpty(file);
        if (!File.Exists(file)) return Utils.TraceOut(true);
        if (!Utils.File.IsVideo(file) && !Utils.File.IsSubtitles(file)) return Utils.TraceOut(true);
        if (!config.DirectoriesRenameVideoFilesOnOff || !scan) return Utils.TraceOut(true);

        ct.ThrowIfCancellationRequested();

        try
        {
            return Utils.TraceOut(Utils.MediaHelper.CheckVideoFileAndRenameIfRequired(ref file));
        }
        catch (Exception ex)
        {
            if (ex is not (IOException or NotSupportedException)) throw;

            return Utils.TraceOut(false);
        }
    }

    /// <summary>
    ///     Return True if all ok otherwise False
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static bool ProcessFilesFixedSpaceCheck(ref string file)
    {
        Utils.TraceIn();
        if (!Utils.StringContainsFixedSpace(file)) return Utils.TraceOut(true);

        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.PathHasAFixedSpace, file));

        try
        {
            file = Utils.RenameFileToRemoveFixedSpaces(file);
            return Utils.TraceOut(true);
        }
        catch (Exception ex)
        {
            if (ex is not (IOException or NotSupportedException)) throw;

            Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.FileIsLocked, file));

            {
                return Utils.TraceOut(false);
            }
        }
    }

    /// <summary>
    ///     Returns True if all ok and False if any issues
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static bool ProcessFilesMaxPathCheck(string file)
    {
        Utils.TraceIn();
        if (file.Length <= Utils.MAX_PATH) return Utils.TraceOut(true);

        Utils.LogWithPushover(BackupAction.Error, string.Format(Resources.PathTooLong, file));

        {
            return Utils.TraceOut(false);
        }
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
            Utils.LogWithPushover(BackupAction.ProcessFiles, rule.Priority, $"{rule.Name} {rule.Message} {file}");

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

        if (oldestFile.DiskCheckedTime != null)
        {
            var days = DateTime.Today.Subtract(oldestFile.DiskCheckedTime.Value).Days;
            oldestBackupDiskAgeTextBox.TextWithInvoke(string.Format(Resources.UpdateOldestBackupDiskNDaysAgo, days, days == 1 ? string.Empty : "s"));
        }
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

            Utils.DiskSpeedTest(rootDirectory, Utils.ConvertMBtoBytes(mediaBackup.Config.SpeedTestFileSize), mediaBackup.Config.SpeedTestIterations, out readSpeed,
                out writeSpeed, ct);
        }
        var totalBytesOnRootDirectoryDiskFormatted = totalBytesOnRootDirectoryDisk.SizeSuffix();
        var freeSpaceOnRootDirectoryDiskFormatted = freeSpaceOnRootDirectoryDisk.SizeSuffix();
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
        var files = Utils.File.GetFiles(rootDirectory, filters, SearchOption.TopDirectoryOnly, 0, 0, ct);

        var filtersToDelete = mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") }).Select(static t => $"^{t.replace}$")
            .ToArray();

        foreach (var file in files)
        {
            Utils.Trace($"Checking {file}");
            _ = CheckForFilesToDelete(file, filtersToDelete);
        }
        Utils.TraceOut();
    }

    internal void ScanAllDirectoriesAsync(CancellationToken ct)
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
        fileBlockingCollection = [];
        directoryScanBlockingCollection = [];

        // split the directories into group by the disk name
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.DirectoriesToBackup);
        RootDirectoryChecks(mediaBackup.Config.DirectoriesToBackup, ct);
        var tasks = new List<Task>(diskNames.Length);

        tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.DirectoriesToBackup)).Select(directoriesOnDisk =>
        {
            return TaskWrapper(() => GetFilesAsync(directoriesOnDisk, scanId, ct), ct);
        }));
        Task.WhenAll(tasks).Wait(ct);
        Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.Completed, true);

        foreach (var scan in directoryScanBlockingCollection)
        {
            mediaBackup.DirectoryScans.Add(scan);
        }
        mediaBackup.ClearFlags();
        Utils.LogWithPushover(BackupAction.ScanDirectory, $"Rename Video Files For Full Scans is {config.DirectoriesRenameVideoFilesForFullScansOnOff}");

        if (!ProcessFiles(fileBlockingCollection, scanId, config.DirectoriesRenameVideoFilesForFullScansOnOff, false, ct))
            Utils.LogWithPushover(BackupAction.ScanDirectory, Resources.ScanDirectoriesFailed);
        else
        {
            // only update the last full scan date if ProcessFiles returned True
            if (updateLastFullScan) mediaBackup.UpdateLastFullScan();
        }
        RemoveOrDeleteFiles(mediaBackup.BackupFiles.Where(static b => !b.Flag).ToArray());
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
            Utils.LogWithPushover(BackupAction.ScanDirectory, string.Format(Resources.Scanning, directory));
            ct.ThrowIfCancellationRequested();
            UpdateStatusLabel(ct, string.Format(Resources.Scanning, directory));
            var files = Utils.File.GetFiles(directory, filters, SearchOption.AllDirectories, 0, 0, ct);
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

    internal void ScanDirectoryAsync(string directory, CancellationToken ct)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(ct);
            ReadyToScan(new FileSystemWatcherEventArgs(directory), SearchOption.AllDirectories, config.DirectoriesRenameVideoFilesForFullScansOnOff, ct);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    internal void ProcessFilesAsync(CancellationToken token)
    {
        try
        {
            Utils.TraceIn();
            if (longRunningActionExecutingRightNow) return;

            DisableControlsForAsyncTasks(token);
            var files = mediaBackup.BackupFiles.Where(static file => !file.Deleted).Select(static file => file.FullPath).ToList();
            var scanId = Guid.NewGuid().ToString();
            mediaBackup.ClearFlags();
            _ = ProcessFiles(files, scanId, config.DirectoriesRenameVideoFilesForFullScansOnOff, false, token);
            ResetAllControls();
        }
        finally
        {
            Utils.TraceOut();
        }
    }

    /// <summary>
    ///     Returns True if the file was deleted. The full path of the file is checked to the array of Regexs provided.
    /// </summary>
    /// <param name="filePath">The full path to the file.</param>
    /// <param name="filters">The Regex filters to find files to delete</param>
    /// <returns></returns>
    private static bool CheckForFilesToDelete(string filePath, IEnumerable<string> filters)
    {
        if (!filters.Any(pattern => Regex.IsMatch(filePath, pattern))) return false;

        Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.High, $"File matches Regex and so will be deleted {filePath}");
        _ = Utils.File.Delete(filePath);
        return true;
    }
}