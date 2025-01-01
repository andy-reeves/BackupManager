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
        _testDataPath = Path.Combine(Utils.GetProjectPath(typeof(MediaHelperTests)), "TestData");
    }

    [Fact]
    public void Test()
    {
        // Step 1 - set up the directories and config
        var targetDirectory = Path.Combine(_testDataPath, "FullTestARunning");
        if (Directory.Exists(targetDirectory)) _ = Utils.Directory.Delete(targetDirectory, true);
        Utils.Directory.Copy(Path.Combine(_testDataPath, "FullTestA"), targetDirectory);
        _ = Directory.CreateDirectory(Path.Combine(targetDirectory, @"BackupDisk 1001\backup 1001\_Movies\EmptyDirectory"));
        var mediaBackup = MediaBackup.Load(Path.Combine(targetDirectory, "ConfigA\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
        Utils.MediaBackup = mediaBackup;
        var ct = CancellationToken.None;

        // Step 2 - Scan the 2 directories and Process Files
        var mainForm = new Main();
        mainForm.ScanAllDirectoriesAsync(ct);
        mainForm.ScanDirectoryAsync(mediaBackup.Config.DirectoriesToBackup[0], ct);
        mainForm.ProcessFilesAsync(ct);

        // Step 3 - Assert state after scan

        // file8 should be renamed
        var file8PathOnSource = Path.Combine(targetDirectory, @"DirectoryB\_TV\File8 {tvdb-250487}\Season 1\File8 s01e01 [Bluray-1080p Remux][DTS-HD MA 5.1][h264].mkv");
        Assert.True(File.Exists(file8PathOnSource));

        var file8SrtPathOnSource = Path.Combine(targetDirectory,
            @"DirectoryB\_TV\File8 {tvdb-250487}\Season 1\File8 s01e01 [Bluray-1080p Remux][DTS-HD MA 5.1][h264].en.srt");
        Assert.True(File.Exists(file8SrtPathOnSource));
        Assert.Equal(6, mediaBackup.BackupFiles.Count);
        Assert.Equal(10, mediaBackup.DirectoryScans.Count);
        Assert.NotNull(mediaBackup.DirectoriesLastFullScan);

        // Step 4 - Check BackupDisks
        const string backupDisk = "BackupDisk 1001";
        Utils.Config.BackupDisk = Path.Combine(targetDirectory, backupDisk);
        mainForm.UpdateBackupDiskTextBoxFromConfig();

        // should be 3 files on the disk
        var files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(4, files.Length);

        // should be still 3 files on the disk
        mainForm.CheckConnectedDiskAndCopyFilesAsync(false, false, ct);
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(4, files.Length);

        // set timestamp to be the same on both files
        var timestampToUse = DateTime.Now;
        var file5PathOnSource = Path.Combine(targetDirectory, @"DirectoryB\_Movies\File5.txt");
        Assert.True(Utils.File.SetLastWriteTime(file5PathOnSource, timestampToUse));
        var backupFile = mediaBackup.BackupFiles.Single(static f => f.FileName == "File5.txt");
        backupFile.UpdateLastWriteTime();
        var file5PathOnBackupDisk = Path.Combine(Utils.Config.BackupDisk, @"backup 1001\_Movies\File5.txt");
        Assert.True(Utils.File.SetLastWriteTime(file5PathOnBackupDisk, timestampToUse));

        //  because the file only differed by hashcode so wasn't deleted but extra file was
        _ = mainForm.CheckConnectedDisk(true, ct);
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(3, files.Length);

        // stamp the backup disk file again now
        file5PathOnBackupDisk = Path.Combine(Utils.Config.BackupDisk, @"backup 1001\_Movies\File5.txt");
        Assert.True(Utils.File.SetLastWriteTime(file5PathOnBackupDisk, DateTime.Now));
        _ = mainForm.CheckConnectedDisk(true, ct);

        // should be only 1 file now as the timestamp was different and it was deleted
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(2, files.Length);

        // Step 5 - Assert status after checking disk with delete option
        Assert.Equal(4, mediaBackup.GetBackupFilesWithDiskEmpty().Count());

        // Step 6 - Copy files
        mainForm.CopyFiles(true, ct);

        // Step 7 - Assert
        // only 1 srt file because we didn't change it on the scan where we renamed the file
        Assert.Empty(mediaBackup.GetBackupFilesWithDiskEmpty());
        var path4 = Utils.Config.BackupDisk;
        files = Utils.File.GetFiles(path4, "*", SearchOption.AllDirectories, 0, 0, ct);
        Assert.Equal(6, files.Length);

        // Now remove a file from the backup disk and check it again to check we detect the deletion correctly
        _ = Utils.File.Delete(file5PathOnBackupDisk);
        _ = mainForm.CheckConnectedDisk(true, ct);

        // now delete one of the files from the source directory and scan again
        _ = Utils.File.Delete(file5PathOnSource);
        _ = mainForm.CheckConnectedDisk(true, ct);
        Assert.Equal(2, mediaBackup.GetBackupFilesWithDiskEmpty().Count());
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(4, files.Length);
        var file4PathOnBackupDisk = Path.Combine(Utils.Config.BackupDisk, @"backup 1001\_Movies\File4.txt");
        var file4PathOnSourceDisk = Path.Combine(targetDirectory, @"DirectoryB\_Movies\File4.txt");
        var destFileName = file4PathOnBackupDisk + "toRename";
        Assert.True(Utils.File.Copy(file4PathOnBackupDisk, destFileName, ct));
        _ = mainForm.CheckConnectedDisk(true, ct);
        Assert.Equal(2, mediaBackup.GetBackupFilesWithDiskEmpty().Count());
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(4, files.Length);
        var destFileName1 = file4PathOnBackupDisk + "toRename";
        Assert.True(Utils.File.Move(file4PathOnBackupDisk, destFileName1));
        _ = mainForm.CheckConnectedDisk(true, ct);
        Assert.Equal(2, mediaBackup.GetBackupFilesWithDiskEmpty().Count());
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(4, files.Length);

        // now delete a file from the source and check the disk again
        Assert.True(Utils.File.Delete(file4PathOnSourceDisk));
        _ = Directory.CreateDirectory(Path.Combine(targetDirectory, backupDisk, @"backup 1001\_Movies\EmptyDirectory"));
        _ = mainForm.CheckConnectedDisk(true, ct);
        Assert.Equal(2, mediaBackup.GetBackupFilesWithDiskEmpty().Count());
        files = Utils.File.GetFiles(Utils.Config.BackupDisk, ct);
        Assert.Equal(3, files.Length);
        _ = mainForm.EnsureConnectedBackupDisk("backup 1001");
        mainForm.ScanAllDirectoriesAsync(ct);
        _ = mainForm.CheckConnectedDisk(true, ct);
        mainForm.CopyFiles(true, ct);
        Assert.Empty(mediaBackup.GetBackupFilesWithDiskEmpty().ToArray());
        mainForm.CheckForOldBackupDisks_Click(null, null);
        mainForm.ScanAllDirectoriesButton_Click(null, null);

        // and now remove the directory we created
        if (Directory.Exists(targetDirectory)) _ = Utils.Directory.Delete(targetDirectory, true);
    }
}