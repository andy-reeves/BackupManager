// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="FullTestA.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class FullTestA
{
    private static readonly string _testDataPath;

    static FullTestA()
    {
        _testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaInfoTests)), "TestData");
    }

    [Fact]
    public void Test()
    {
        // Step 1 - set up the directories and config
        var targetDirectory = Path.Combine(_testDataPath, "FullTestARunning");
        if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
        Utils.CopyDirectory(Path.Combine(_testDataPath, "FullTestA"), targetDirectory);
        var mediaBackup = MediaBackup.Load(Path.Combine(targetDirectory, "ConfigA\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
        Utils.MediaBackup = mediaBackup;
        var ct = new CancellationToken();

        // Step 2 - Scan the 2 directories and Process Files
        var mainForm = new Main();
        mainForm.ScanAllDirectories(true, ct);

        // Step 3 - Assert state after scan
        Assert.Equal(5, mediaBackup.BackupFiles.Count);
        Assert.Equal(6, mediaBackup.DirectoryScans.Count);
        Assert.NotNull(mediaBackup.DirectoriesLastFullScan);

        // Step 4 - Check BackupDisks
        Utils.Config.BackupDisk = Path.Combine(targetDirectory, "BackupDisk 1001");
        mainForm.UpdateBackupDiskTextBoxFromConfig();

        // should be 2 files on the disk
        var files = Utils.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(2, files.Length);

        // should be still 2 files on the disk
        _ = mainForm.CheckConnectedDisk(false, ct);
        files = Utils.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(2, files.Length);

        // set timestamp to be the same on both files
        var timestampToUse = DateTime.Now;
        var file5PathOnSource = Path.Combine(targetDirectory, @"DirectoryB\_Movies\File5.txt");
        Assert.True(Utils.SetFileLastWriteTime(file5PathOnSource, timestampToUse));
        var backupFile = mediaBackup.BackupFiles.Single(static f => f.FileName == "File5.txt");
        backupFile.UpdateLastWriteTime();
        var file5Path = Path.Combine(Utils.Config.BackupDisk, @"backup 1001\_Movies\File5.txt");
        Assert.True(Utils.SetFileLastWriteTime(file5Path, timestampToUse));

        // should still be 2 because the file only differed by hashcode so wasn't deleted
        _ = mainForm.CheckConnectedDisk(true, ct);
        files = Utils.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(2, files.Length);

        // stamp the backup disk file again now
        file5Path = Path.Combine(Utils.Config.BackupDisk, @"backup 1001\_Movies\File5.txt");
        Assert.True(Utils.SetFileLastWriteTime(file5Path, DateTime.Now));
        _ = mainForm.CheckConnectedDisk(true, ct);

        // should be only 1 file now as the timestamp was different and it was deleted
        files = Utils.GetFiles(Utils.Config.BackupDisk, ct);
        _ = Assert.Single(files);

        // Step 5 - Assert status after checking disk with delete option
        Assert.Equal(4, mediaBackup.GetBackupFilesWithDiskEmpty().Count());

        // Step 6 - Copy files
        mainForm.CopyFiles(true, ct);

        // Step 7 - Assert
        Assert.Empty(mediaBackup.GetBackupFilesWithDiskEmpty());

        // should be 5 now after a copy
        files = Utils.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(5, files.Length);
        if (Directory.Exists(targetDirectory)) Directory.Delete(targetDirectory, true);
    }
}