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
        var files = Utils.File.GetFiles(directoryToCheck, mediaBackup.GetFilters(), searchOption, ct);
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
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.DirectoriesToBackup);
        var tasks = new List<Task<bool>>(diskNames.Length);
        fileCounterForMultiThreadProcessing = 0;
        EnableProgressBar(0, filesParam.Count);
        var suffix = filesParam.Count == 1 ? string.Empty : "s";
        Utils.LogWithPushover(BackupAction.ProcessFiles, PushoverPriority.Normal, $"Processing {filesParam.Count:n0} file{suffix}", false, true);

        // One process thread for each disk that has files on it to scan
        tasks.AddRange(diskNames.Select(diskName => Utils.GetFilesForDisk(diskName, filesParam)).Select(files => TaskWrapper(
            Task.Run(() => files == null || files.Length == 0 || ProcessFilesInternal(files, scanId, scanPathForVideoCodec, ct), ct), ct)));

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
    /// <param name="ct"></param>
    private bool ProcessFilesInternal(IEnumerable<string> filesParam, string scanId, bool scanPathForVideoCodec, CancellationToken ct)
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
            try
            {
                var file = fileInsideForEach;

                lock (_lock)
                {
                    if (!ProcessFilesMaxPathCheck(file)) continue;
                    if (!ProcessFilesFixedSpaceCheck(ref file)) continue;
                    if (!ProcessFilesRenameFileRules(ref file)) continue;
                    if (!ProcessFilesCheckAllMediaInfo(scanPathForVideoCodec, ref file, ct)) continue;
                    if (file == null) continue;

                    if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
                    if (files.Length > 50) ProcessFilesUpdatePercentComplete(file);
                }
                directoryScanning = DirectoryScanning(scanId, file, directoryScanning, files, ref firstDir, ref scanInfo);
                UpdateStatusLabel(ct, Resources.Processing, fileCounterForMultiThreadProcessing);
                if (CheckForFilesToDelete(file, filtersToDelete)) continue;

                ProcessFileRules(file);
                _ = mediaBackup.EnsureFile(file);
            }
            catch (IOException ex)
            {
                // exception accessing the file return false and we will try again 
                Utils.Trace($"IOException in ProcessFilesInternal {ex}");
                return Utils.TraceOut(false);
            }
        }

        // Update the last scan endDateTime as it wasn't set in the loop
        if (scanInfo != null) scanInfo.EndDateTime = DateTime.Now;
        return Utils.TraceOut(true);
    }

    private string DirectoryScanning(string scanId, string file, string directoryScanning, IEnumerable<string> files, ref bool firstDir,
        ref DirectoryScan scanInfo)
    {
        _ = mediaBackup.GetFoldersForPath(file, out var directory, out _);
        if (directory == directoryScanning) return directoryScanning;

        if (!firstDir) scanInfo.EndDateTime = DateTime.Now;

        scanInfo = new DirectoryScan(DirectoryScanType.ProcessingFiles, directory, DateTime.Now, scanId)
        {
            TotalFiles = files.Count(f => f.StartsWithIgnoreCase(directory))
        };
        mediaBackup.DirectoryScans.Add(scanInfo);
        directoryScanning = directory;
        firstDir = false;
        return directoryScanning;
    }

    private string[] FiltersToDelete()
    {
        return mediaBackup.Config.FilesToDelete
            .Select(static filter => new { filter, replace = filter.Replace(".", @"\.").Replace("*", ".*").Replace("?", ".") })
            .Select(static t => $"^{t.replace}$").ToArray();
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

        foreach (var fileRenameRule in config.FileRenameRules
                     .Select(static fileRenameRule => new { fileRenameRule, a = fileRenameRule.FileDiscoveryRegex })
                     .Where(static t => !t.a.HasNoValue()).Select(t => new { t, match = Regex.Match(newFilePath, t.a) })
                     .Where(static t => t.match.Success).Select(static t => t.t.fileRenameRule))
        {
            newFilePath = fileRenameRule.Search.Split(',').Aggregate(newFilePath, (current, b) => current.Replace(b, fileRenameRule.Replace));
        }
        if (newFilePath == file) return Utils.TraceOut(true);

        Utils.Log(BackupAction.ProcessFiles, $"{file} being renamed to {newFilePath}");
        if (!Utils.File.Move(file, newFilePath)) return Utils.TraceOut(false);

        file = newFilePath;
        return Utils.TraceOut(true);
    }

    private void ProcessFilesUpdatePercentComplete(string file)
    {
        fileCounterForMultiThreadProcessing++;
        currentPercentComplete = fileCounterForMultiThreadProcessing * 100 / toolStripProgressBar.Maximum;
        if (currentPercentComplete % 20 != 0 || currentPercentComplete <= reportedPercentComplete) return;

        reportedPercentComplete = currentPercentComplete;
        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.ProcessingPercentage, currentPercentComplete), true, true);
        Utils.Trace($"{fileCounterForMultiThreadProcessing} Processing {file}");
    }

    private bool ProcessFilesCheckAllMediaInfo(bool scan, ref string file, CancellationToken ct)
    {
        Utils.TraceIn();
        ArgumentException.ThrowIfNullOrEmpty(file);
        if (!File.Exists(file) || !Utils.File.IsVideo(file) || !config.DirectoriesRenameVideoFilesOnOff || !scan) return Utils.TraceOut(true);

        if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

        try
        {
            Utils.MediaHelper.CheckVideoFileAndRenameIfRequired(ref file);
        }
        catch (Exception ex)
        {
            if (ex is not (IOException or NotSupportedException)) throw;

            Utils.Log(BackupAction.ProcessFiles, $"Exception was {ex}");
            Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.FileIsLocked, file));
            return Utils.TraceOut(false);
        }
        return Utils.TraceOut(true);
    }
    /*
    /// <summary>
    ///     Returns True if all OK otherwise False
    /// </summary>
    /// <param name="scan"></param>
    /// <param name="file"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    private bool ProcessFilesVideoCodecCheck(bool scan, ref string file, CancellationToken ct)
    {
        Utils.TraceIn();
        if (!File.Exists(file) || !Utils.FileIsVideo(file) || !config.DirectoriesRenameVideoFilesOnOff || !scan) return Utils.TraceOut(true);

        try
        {
            if (Utils.RenameVideoCodec(file, out var newFile, out var oldCodec, out var newCodec))
            {
                Utils.Log(BackupAction.ProcessFiles, $"{file} had codec of {oldCodec} in its path and now has {newCodec}");
                Utils.Log(BackupAction.ProcessFiles, string.Format(Resources.FileRenameRequiredForVideoCodec, Path.GetFileName(file)));

                // change the file to the newFile to continue processing
                file = newFile;

                // find any files in this folder that end in one of our srt valid extensions
                var oldCodecInBrackets = oldCodec.WrapInSquareBrackets();
                var newCodecInBrackets = newCodec.WrapInSquareBrackets();
                var directoryPath = Path.GetDirectoryName(file);
                var filesInSameDirectory = Utils.GetFiles(directoryPath, ct);

                foreach (var f in filesInSameDirectory.Where(static f => f.ContainsAny(Utils.SubtitlesExtensions))
                             .Where(f => f.Contains(oldCodecInBrackets)))
                {
                    Utils.Log(BackupAction.ProcessFiles, $"{f} had codec of {oldCodec} in its path and will be renamed with {newCodec}");
                    var newName = f.Replace(oldCodecInBrackets, newCodecInBrackets);
                    if (!Utils.FileMove(f, newName)) return Utils.TraceOut(false);
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is not (IOException or NotSupportedException)) throw;

            Utils.Log(BackupAction.ProcessFiles, $"Exception was {ex}");
            Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.FileIsLocked, file));
            return Utils.TraceOut(false);
        }
        return Utils.TraceOut(true);
    }*/

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

        Utils.LogWithPushover(BackupAction.ProcessFiles, string.Format(Resources.PathTooLong, file));

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
        var files = Utils.File.GetFiles(rootDirectory, filters, SearchOption.TopDirectoryOnly, 0, 0, ct);

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
        fileBlockingCollection = new BlockingCollection<string>();
        directoryScanBlockingCollection = new BlockingCollection<DirectoryScan>();

        // split the directories into group by the disk name
        var diskNames = Utils.GetDiskNames(mediaBackup.Config.DirectoriesToBackup);
        RootDirectoryChecks(mediaBackup.Config.DirectoriesToBackup, ct);
        var tasks = new List<Task>(diskNames.Length);

        tasks.AddRange(diskNames.Select(diskName => Utils.GetDirectoriesForDisk(diskName, mediaBackup.Config.DirectoriesToBackup))
            .Select(directoriesOnDisk => { return TaskWrapper(() => GetFilesAsync(directoriesOnDisk, scanId, ct), ct); }));
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
            Utils.LogWithPushover(BackupAction.ScanDirectory, PushoverPriority.Normal, string.Format(Resources.Scanning, directory));
            if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();
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

            ReadyToScan(new FileSystemWatcherEventArgs(directory), SearchOption.AllDirectories,
                config.DirectoriesRenameVideoFilesForFullScansOnOff, ct);
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
        _ = Utils.File.Delete(filePath);
        return true;
    }
}