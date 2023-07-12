
namespace BackupManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using System.Configuration;
    using BackupManager.Entities;
    using System.Diagnostics;

    public partial class Main : Form
    {
        #region Fields

        private readonly MediaBackup mediaBackup;

        private const int DiskSpeedTestFileSize = 200 * Utils.BytesInOneMegabyte;
        private const int DiskSpeedTestIterations = 1;

        #endregion

        private DailyTrigger trigger;

        private Action triggerAction;

        // When the serice monitoring has been enabled this is True
        private bool serviceMonitoringRunning;

#if DEBUG
        private string logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManagerDebug.log");
#endif

#if !DEBUG
        private string logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager.log");
#endif
        #region Constructors and Destructors

        public Main()
        {
            InitializeComponent();
 
#if DEBUG
            Trace.Listeners.Add(new TextWriterTraceListener(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"BackupManagerTrace.log"), "myListener"));
#endif

            Utils.Trace("Trace is on");

            string MediabackupXml = ConfigurationManager.AppSettings.Get("MediabackupXml");

            string localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");

            mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : MediabackupXml);

            mediaBackup.LogParameters(logFile);

#if !DEBUG
            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");
#endif

            foreach (string a in mediaBackup.MasterFolders)
            {
                masterFoldersComboBox.Items.Add(a);
            }

            foreach (string a in mediaBackup.MasterFolders)
            {
                restoreMasterFolderComboBox.Items.Add(a);
            }

            triggerAction = () => { ScheduledBackup(); };

            DateTime startTime = DateTime.Parse(mediaBackup.ScheduledBackupStartTime);

            hoursNumericUpDown.Value = startTime.Hour;

            minutesNumericUpDown.Value = startTime.Minute;

            if (mediaBackup.StartScheduledBackup)
            {
                monitoringButton_Click(null, null);
                timerButton_Click(null, null);
            }

            Utils.Trace("Main exit");
        }

        #endregion

        #region Methods

        private void ListFilesWithoutBackupDiskCheckedButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("ListFilesWithoutBackupDiskCheckedButton_Click enter");

            Utils.Log(logFile, "Listing files without DiskChecked");

            foreach (BackupFile file in mediaBackup.GetBackupFilesWithDiskCheckedEmpty())
            {
                Utils.Log(logFile, $"{file.FullPath} does not have DiskChecked set on disk {file.Disk}");
            }
            Utils.Trace("ListFilesWithoutBackupDiskCheckedButton_Click exit");

        }

        private void CheckConnectedBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("CheckConnectedBackupDriveButton_Click enter");

            CheckConnectedDisk(false);

            Utils.Trace("CheckConnectedBackupDriveButton_Click exit");
        }

        private void CheckConnectedDisk(bool deleteExtraFiles)
        {
            Utils.Trace("CheckConnectedDisk enter");

            // Scans the connected backup disk and finds all its files
            // for each for found calculate the hash from the backup disk
            // find that hash in the backup data file
            // rebuilds the source filename from MasterFolder+IndexFolder+Path
            // checks the file still exists there
            // if it does compare the hashcodes and update results
            // force a recalc of both the hashes to check the files can both be read correctly

            Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Started");

            string backupShare = backupDiskTextBox.Text;

            // check the backupDisks has an entry for this disk
            BackupDisk disk = mediaBackup.GetBackupDisk(backupShare);
            if (disk == null)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup share {backupShare}");
                return;
            }

            var result = disk.Update(mediaBackup.BackupFiles);

            if (!result)
            {   // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
                // if not thats a problem
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}");
                return;
            }

            string folderToCheck = disk.BackupPath;

            long readSpeed, writeSpeed;

            Utils.DiskSpeedTest(folderToCheck, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);

            string text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nRead: {Utils.FormatSpeed(readSpeed)}\nWrite: {Utils.FormatSpeed(writeSpeed)}";

            bool diskInfoMessageWasTheLastSent = true;

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.CheckBackupDisk,
                                  text);

            if (disk.Free < mediaBackup.MinimumCriticalBackupDiskSpace * Utils.BytesInOneGigabyte)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      PushoverPriority.High,
                                      $"{disk.FreeFormatted} free is very low. Prepare new backup disk");
            }

            IEnumerable<BackupFile> filesToReset = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name);

            foreach (BackupFile file in filesToReset)
            {
                file.ClearDiskChecked();
            }

            string[] backupDiskFiles = Utils.GetFiles(
                folderToCheck,
                "*",
                SearchOption.AllDirectories,
                FileAttributes.Hidden);

            foreach (string backupFileFullPath in backupDiskFiles)
            {
                string backupFileIndexFolderRelativePath = backupFileFullPath.Substring(folderToCheck.Length + 1);

                if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
                {
                    BackupFile backupFile = mediaBackup.GetBackupFile(backupFileIndexFolderRelativePath);

                    // This happens when the file we have on the backup disk is no longer in the masterFolder
                    if (File.Exists(backupFile.FullPath))
                    {
                        // This forces a hash check on the source and backup disk files
                        Utils.Trace($"Checking hash for {backupFile.Hash}");
                        bool returnValue = backupFile.CheckContentHashes(disk);

                        if (returnValue == false)
                        {
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                     mediaBackup.PushoverAppToken,
                                     logFile,
                                     BackupAction.CheckBackupDisk, PushoverPriority.High,
                                     $"There was an error with the hashcodes on the source and backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath} or the file was partially copied last time. It could be that the source file or destination file are corrupted though.");

                            diskInfoMessageWasTheLastSent = false;
                            continue;
                        }
                        else
                        {
                            continue;
                        }
                    }
                }

                // Extra file on a backup disk
                if (deleteExtraFiles)
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.CheckBackupDisk,
                                          $"Extra file {backupFileFullPath} on backup disk {disk.Name} now deleted");

                    Utils.ClearFileAttribute(backupFileFullPath, FileAttributes.ReadOnly);
                    File.Delete(backupFileFullPath);
                    diskInfoMessageWasTheLastSent = false;
                }
                else
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.CheckBackupDisk,
                                          $"Extra file {backupFileFullPath} on backup disk {disk.Name}");
                    diskInfoMessageWasTheLastSent = false;
                }

            }

            DeleteEmptyDirectories(logFile, folderToCheck);

            disk.UpdateDiskChecked();

            result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}"
                                      );
                return;
            }

            mediaBackup.Save();

            if (!diskInfoMessageWasTheLastSent)
            {
                text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";

                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      text);
            }
            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.CheckBackupDisk,
                                  "Completed");

            Utils.Trace("CheckConnectedDisk exit");
        }

        private void DeleteEmptyDirectories(string logFile, string directory)
        {
            Utils.Trace("DeleteEmptyDirectories enter");

            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("Directory is a null reference or an empty string", "directory");
            }
            try
            {
                foreach (var subDirectory in Directory.EnumerateDirectories(directory))
                {
                    DeleteEmptyDirectories(logFile, subDirectory);
                }

                var entries = Directory.EnumerateFileSystemEntries(directory);

                if (!entries.Any())
                {
                    try
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                              mediaBackup.PushoverAppToken,
                                              logFile,
                                              BackupAction.CheckBackupDisk,
                                              $"Deleting empty folder {directory}");
                        Directory.Delete(directory);
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (DirectoryNotFoundException) { }
                }
            }
            catch (UnauthorizedAccessException) { }

            Utils.Trace("DeleteEmptyDirectories exit");
        }

        private bool EnsureConnectedBackupDisk(string backupDisk, string logFile)
        {
            Utils.Trace("EnsureConnectedBackupDisk enter");

            // checks the specified backup disk is connected already and returns if it is
            //if its not it prompts the user to insert correct disk and waits
            // user clicks 'Yes' inserted and then returns

            if (!BackupDisk.CheckForValidBackupShare(backupDiskTextBox.Text))
            {
                return false;
            }

            string currentConnectedBackupDiskName = BackupDisk.GetBackupFolderName(backupDiskTextBox.Text);
            while (currentConnectedBackupDiskName != backupDisk)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.General,
                                      PushoverPriority.High,
                                      $"Connect new backup drive to restore from {backupDisk}"
                                      );

                DialogResult answer =
            MessageBox.Show($"Please connect backup disk {backupDisk} so we can continue restoring files. Have you connected this disk now?",
                            "Connect correct backup disk",
                            MessageBoxButtons.YesNo);
                if (answer == DialogResult.No)
                {
                    return false;
                }

                if (answer == DialogResult.Yes)
                {
                    currentConnectedBackupDiskName = BackupDisk.GetBackupFolderName(backupDiskTextBox.Text);
                }
            }

            Utils.Trace("EnsureConnectedBackupDisk exit");

            return true;
        }

        private void CopyFilesToBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("CopyFilesToBackupDiskButton_Click enter");

            CopyFiles();

            Utils.Trace("CopyFilesToBackupDiskButton_Click exit");
        }

        private void CopyFiles()
        {
            Utils.Trace("CopyFiles enter");

            string backupShare = backupDiskTextBox.Text;

            // check the backupDisks has an entry for this disk
            BackupDisk disk = mediaBackup.GetBackupDisk(backupShare);
            if (disk == null)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup share {backupShare}"
                                      );
                return;
            }

            var result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup {disk.Name}"
                                      );
                return;
            }

            string insertedBackupDrive = disk.BackupPath;

            Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "Started");

            IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(q => q.Length);

            long sizeOfFiles = filesToBackup.Sum(x => x.Length);

            int totalFileCount = filesToBackup.Count();

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.BackupFiles,
                                  $"{totalFileCount:n0} files to backup at {Utils.FormatSize(sizeOfFiles)}"
                                 );

            bool outOfDiskSpaceMessageSent = false;

            int fileCounter = 0;

            foreach (BackupFile backupFile in filesToBackup)
            {
                try
                {
                    fileCounter++;

                    if (string.IsNullOrEmpty(backupFile.IndexFolder))
                    {
                        throw new ApplicationException("Index folder is empty");
                    }

                    string destinationFileName = Path.Combine(insertedBackupDrive, backupFile.IndexFolder, backupFile.RelativePath);
                    string sourceFileName = backupFile.FullPath;
                    FileInfo sourceFileInfo = new FileInfo(sourceFileName);
                    string sourceFileSize = Utils.FormatSize(sourceFileInfo.Length);

                    if (File.Exists(destinationFileName))
                    {
                        Utils.Log(logFile, $"[{fileCounter}/{totalFileCount}] Skipping copy as it exists");

                        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                        // in which case check the source hash again and then check the copied file 
                        bool returnValue = backupFile.CheckContentHashes(disk);

                        if (returnValue == false)
                        {
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                     mediaBackup.PushoverAppToken,
                                     logFile,
                                     BackupAction.CheckBackupDisk, PushoverPriority.High,
                                     $"There was an error with the hashcodes on the source and backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath}");
                        }
                    }
                    else
                    {
                        long availableSpace;
                        long totalBytes;
                        result = Utils.GetDiskInfo(backupShare, out availableSpace, out totalBytes);

                        if (availableSpace > (mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk * Utils.BytesInOneMegabyte))
                        {
                            if (availableSpace > sourceFileInfo.Length)
                            {
                                outOfDiskSpaceMessageSent = false;
                                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                      mediaBackup.PushoverAppToken,
                                                      logFile,
                                                      BackupAction.BackupFiles,
                                                      $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nCopying {sourceFileName} at {sourceFileSize}"
                                                      );

                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));

                                DateTime startTime = DateTime.UtcNow;
                                File.Copy(sourceFileName, destinationFileName);
                                DateTime endTime = DateTime.UtcNow;

                                string copySpeed = Utils.FormatSpeed(Convert.ToInt64(sourceFileInfo.Length / (endTime - startTime).TotalSeconds));

                                Utils.Trace($"Copy complete at {copySpeed}");

                                // Make sure its not readonly
                                Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                                // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                                // in which case check the source hash again and then check the copied file 
                                backupFile.UpdateContentsHash();
                                bool returnValue = backupFile.CheckContentHashes(disk);

                                if (returnValue == false)
                                {
                                    // There was an error with the hashcodes of the source file anf the file on the backup disk
                                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                             mediaBackup.PushoverAppToken,
                                             logFile,
                                             BackupAction.CheckBackupDisk, PushoverPriority.High,
                                             $"There was an error with the hashcodes on the source and backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath}");
                                }
                            }
                            else
                            {
                                if (!outOfDiskSpaceMessageSent)
                                {
                                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                          mediaBackup.PushoverAppToken,
                                                          logFile,
                                                          BackupAction.BackupFiles,
                                                          $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free. Skipping {sourceFileName} as not enough free space for {sourceFileSize}"
                                                          );
                                    outOfDiskSpaceMessageSent = true;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                catch (FileNotFoundException)
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.BackupFiles,
                                          PushoverPriority.High,
                                          $"{backupFile.FullPath} is not found. It's most likely been replaced since our scan."
                                         );
                }

                catch (IOException ex)
                {
                    // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.BackupFiles,
                                          PushoverPriority.Emergency,
                                          $"IOException during copy. Skipping file. Details {ex}"
                                         );
                }
            }

            result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}"
                                      );
                return;
            }

            mediaBackup.Save();

            IEnumerable<BackupFile> filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

            if (filesStillNotOnBackupDisk.Count() > 0)
            {
                sizeOfFiles = filesStillNotOnBackupDisk.Sum(p => p.Length);

                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.BackupFiles,
                                      $"{filesStillNotOnBackupDisk.Count():n0} files still to backup at {Utils.FormatSize(sizeOfFiles)}"
                                      );
            }

            IEnumerable<BackupFile> filesWithoutDiskChecked = mediaBackup.GetBackupFilesWithDiskCheckedEmpty();

            if (filesWithoutDiskChecked.Count() > 0)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.BackupFiles,
                                      $"{filesWithoutDiskChecked.Count():n0} files still without DiskChecked set"
                                      );
            }

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.BackupFiles,
                                  $"{disk.FreeFormatted} free on backup disk"
                                  );

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.BackupFiles,
                                  PushoverPriority.High,
                                  "Completed");
  
            Utils.Trace("CopyFiles exit");
        }

        private void EnsureFile(string path, string masterFolder, string indexFolder)
        {
            Utils.Trace("EnsureFile enter");

            // Empty Index folder is not allowed
            if (string.IsNullOrEmpty(indexFolder))
            {
                throw new ApplicationException("IndexFolder is empty. Not supported");
            }

            BackupFile backupFile = mediaBackup.GetBackupFile(path, masterFolder, indexFolder);

            if (backupFile == null)
            {
                throw new ApplicationException($"Duplicate hashcode detected indicated a copy of a file at {path}");
            }

            if (string.IsNullOrEmpty(backupFile.ContentsHash))
            {
                throw new ApplicationException("ContentHash is null or empty");
            }

            backupFile.Flag = true;

            Utils.Trace("EnsureFile exit");
        }

        private void ListFilesNotOnBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("ListFilesNotOnBackupDiskButton_Click enter");

            IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(p => p.Length);

            Utils.Log(logFile, "Listing files not on a backup disk");
            foreach (BackupFile file in filesNotOnBackupDisk)
            {
                Utils.Log(logFile, $"{file.FullPath} at {Utils.FormatSize(file.Length)}");
            }

            Utils.Trace("ListFilesNotOnBackupDiskButton_Click exit");
        }

        private void recalculateAllHashesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("recalculateAllHashesButton_Click enter");

            DialogResult answer =
                MessageBox.Show(
                    "Are you sure you want to recalculate the hashcodes from the master files?",
                    "Recalculate Hashcodes",
                    MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                foreach (BackupFile backupFile in mediaBackup.BackupFiles)
                {
                    backupFile.UpdateContentsHash();
                }

                mediaBackup.Save();
            }

            Utils.Trace("recalculateAllHashesButton_Click exit");
        }

        private void UpdateMasterFilesButtonClick(object sender, EventArgs e)
        {
            Utils.Trace("UpdateMasterFilesButtonClick enter");

            DialogResult answer = MessageBox.Show(
                "Are you sure you want to rebuild the master list?",
                "Rebuild master list",
                MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                ScanFolders();
            }

            Utils.Trace("UpdateMasterFilesButtonClick exit");
        }

        private void ScanFolders()
        {
            Utils.Trace("ScanFolders enter");

            string filters = string.Join(",", mediaBackup.Filters.ToArray());

            long readSpeed, writeSpeed;

            mediaBackup.ClearFlags();

            Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Started");

            foreach (string masterFolder in mediaBackup.MasterFolders)
            {
                if (!Directory.Exists(masterFolder))
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                     mediaBackup.PushoverAppToken,
                                     logFile,
                                     BackupAction.ScanFolders, PushoverPriority.High,
                                     $"{masterFolder} is not available"
                                     );
                }

                if (Directory.Exists(masterFolder))
                {
                    if (!Utils.IsFolderWritable(masterFolder))
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                         mediaBackup.PushoverAppToken,
                                         logFile,
                                         BackupAction.ScanFolders, PushoverPriority.High,
                                         $"{masterFolder} is not writeable"
                                         );
                    }

                    long freeSpaceOnCurrentMasterFolder;
                    long totalBytesOnMasterFolderDisk;
                    Utils.GetDiskInfo(masterFolder, out freeSpaceOnCurrentMasterFolder, out totalBytesOnMasterFolderDisk);
                    Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);

                    string totalBytesOnMasterFolderDiskFormatted = Utils.FormatSize(totalBytesOnMasterFolderDisk);
                    string freeSpaceOnCurrentMasterFolderFormatted = Utils.FormatSize(freeSpaceOnCurrentMasterFolder);
                    string readSpeedFormatted = Utils.FormatSpeed(readSpeed);
                    string writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);

                    string text = $"{masterFolder}\nTotal: {totalBytesOnMasterFolderDiskFormatted}\nFree: {freeSpaceOnCurrentMasterFolderFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";

                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.ScanFolders,
                                          text
                                          );

                    if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderReadSpeed))
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                         mediaBackup.PushoverAppToken,
                                         logFile,
                                         BackupAction.ScanFolders,
                                         PushoverPriority.High,
                                         $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderReadSpeed))}"
                                         );
                    }

                    if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderWriteSpeed))
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                         mediaBackup.PushoverAppToken,
                                         logFile,
                                         BackupAction.ScanFolders,
                                         PushoverPriority.High,
                                         $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderWriteSpeed))}"
                                         );
                    }

                    if (freeSpaceOnCurrentMasterFolder < Utils.ConvertMBtoBytes(mediaBackup.MinimumCriticalMasterFolderSpace))
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                              mediaBackup.PushoverAppToken,
                                              logFile,
                                              BackupAction.ScanFolders,
                                              PushoverPriority.High,
                                              $"Free space on {masterFolder} is too low"
                                              );
                    }

                    foreach (string indexFolder in mediaBackup.IndexFolders)
                    {
                        string folderToCheck = Path.Combine(masterFolder, indexFolder);

                        if (Directory.Exists(folderToCheck))
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                  mediaBackup.PushoverAppToken,
                                                  logFile,
                                                  BackupAction.ScanFolders,
                                                  $"{folderToCheck}"
                                                  );

                            string[] files = Utils.GetFiles(folderToCheck, filters, SearchOption.AllDirectories);

                            foreach (string file in files)
                            {
                                Utils.Trace($"Checking {file}");

                                // Checks for TV only
                                if (file.Contains("_TV"))
                                {
                                    if (!file.Contains("tvdb") && !file.Contains("tmdb"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"TV Series has missing tvdb-/tmdb- in the filepath {file}"
                                                              );
                                    }

                                    // check that tv files only have directories in the parents parent and no files
                                    FileInfo fileInfo = new FileInfo(file);

                                    FileInfo[] grandparentFiles = fileInfo.Directory.Parent.GetFiles();
                                    if (grandparentFiles != null && grandparentFiles.Count() > 0)
                                    {
                                        // files are allowed here as long as they end with a special feature name suffix
                                        foreach (FileInfo grandparentFile in grandparentFiles)
                                        {
                                            string fName = grandparentFile.Name;
                                            if (!(fName.Contains("-featurette.") ||
                                               fName.Contains("-other.") ||
                                               fName.Contains("-interview.") ||
                                               fName.Contains("-scene.") ||
                                               fName.Contains("-short.") ||
                                               fName.Contains("-deleted.") ||
                                               fName.Contains("-behindthescenes.") ||
                                               fName.Contains("-trailer.")))
                                            {
                                                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                                      mediaBackup.PushoverAppToken,
                                                                      logFile,
                                                                      BackupAction.ScanFolders,
                                                                      PushoverPriority.High,
                                                                      $"TV File {file} has files in the grandparent folder which aren't special features"
                                                                      );
                                            }
                                        }
                                    }

                                    // check the filename doesn't have TBA in it
                                    if (file.Contains("TBA"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                                    mediaBackup.PushoverAppToken,
                                                                    logFile,
                                                                    BackupAction.ScanFolders,
                                                                    PushoverPriority.High,
                                                                    $"TV File {file} has TBA in the filename"
                                                                    );
                                    }
                                }

                                // Checks for Movies, Comedy, Concerts only (main Video folders)
                                if (file.Contains("_Movies") ||
                                    file.Contains("_Concerts") ||
                                    file.Contains("_Comedy"))
                                {
                                    if (!(file.Contains("-featurette.") ||
                                            file.Contains("-other.") ||
                                            file.Contains("-interview.") ||
                                            file.Contains("-scene.") ||
                                            file.Contains("-short.") ||
                                            file.Contains("-deleted.") ||
                                            file.Contains("-behindthescenes.") ||
                                            file.Contains("-trailer.")))
                                    {
                                        if (!file.Contains("tmdb"))
                                        {
                                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                                  mediaBackup.PushoverAppToken,
                                                                  logFile,
                                                                  BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"Movie has missing tmdb- in the filename {file}"
                                                                  );
                                        }

                                        FileInfo movieFileInfo = new FileInfo(file);
                                        if (!movieFileInfo.Name.StartsWith(movieFileInfo.Directory.Name))
                                        {
                                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                                  mediaBackup.PushoverAppToken,
                                                                  logFile,
                                                                  BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"Movie filename doesn't start with the folder name in the filename {file}"
                                                                  );
                                        }
                                    }

                                    //Edition checks '{edition-EXTENDED EDITION}'
                                    if (file.Contains("{edition-"))
                                    {
                                        bool found = false;
                                        foreach (string s in mediaBackup.EditionsAllowed)
                                        {
                                            if (file.Contains("{edition-" + s, StringComparison.OrdinalIgnoreCase))
                                            {
                                                found = true;
                                                break;
                                            }
                                        }
                                        if (!found)
                                        {
                                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                                  mediaBackup.PushoverAppToken,
                                                                  logFile,
                                                                  BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"File has 'edition-' in the filename {file} but no valid edition specification"
                                                                  );
                                        }
                                    }

                                    // check that movies only have files in the directories and no subfolders
                                    FileInfo fileInfo = new FileInfo(file);

                                    DirectoryInfo[] siblingDirectories = fileInfo.Directory.GetDirectories();
                                    if (siblingDirectories != null && siblingDirectories.Count() > 0)
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"File {file} has folders alongside it"
                                                              );
                                    }
                                }

                                // Checks for Movies, TV, Comedy or Concerts (All Video files)
                                if (file.Contains("_TV") ||
                                    file.Contains("_Movies") ||
                                    file.Contains("_Concerts") ||
                                    file.Contains("_Comedy"))
                                {
                                    if (file.Contains("subtitles"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has 'subtitles' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" ()"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has a missing year in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" (0)"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has a '0' year in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" Proper]"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has ' Proper]' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" Proper REAL]"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has ' Proper REAL]' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" REAL]"))
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has ' REAL]' in the filename {file}"
                                                              );
                                    }

                                    bool found = false;
                                    foreach (string s in mediaBackup.VideoFoldersFormatsAllowed)
                                    {
                                        if (file.EndsWith(s, StringComparison.OrdinalIgnoreCase))
                                        {
                                            found = true;
                                            break;
                                        }
                                    }
                                    if (!found)
                                    {
                                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                              mediaBackup.PushoverAppToken,
                                                              logFile,
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has an invalid file extension in the filename {file}"
                                                              );
                                    }
                                }

                                // All files except Backup
                                if (!file.Contains("_Backup"))
                                {
                                    // Placeholder for file scans
                                }

                                EnsureFile(file, masterFolder, indexFolder);
                            }
                        }
                    }
                }
                else
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                          mediaBackup.PushoverAppToken,
                                          logFile,
                                          BackupAction.ScanFolders,
                                          PushoverPriority.High,
                                          $"{masterFolder} doesn't exist"
                                          );
                }
            }

            mediaBackup.RemoveFilesWithFlag(false, true);
            mediaBackup.Save();

            var totalFiles = mediaBackup.BackupFiles.Count();

            long totalFileSize = mediaBackup.BackupFiles.Sum(p => p.Length);

            IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

            long fileSizeToCopy = filesNotOnBackupDisk.Sum(p => p.Length);

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.ScanFolders,
                                  $"{totalFiles:n0} files at {Utils.FormatSize(totalFileSize)}");

            BackupFile oldestFile = mediaBackup.GetOldestFile();

            if (oldestFile != null)
            {
                DateTime oldestFileDate = DateTime.Parse(oldestFile.DiskChecked);

                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.ScanFolders,
                                      $"Oldest backup date is {DateTime.Today.Subtract(oldestFileDate).Days:n0} day(s) ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
            }

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.ScanFolders,
                                  $"{filesNotOnBackupDisk.Count():n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");

            Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Completed");
 
            Utils.Trace("ScanFolders exit");
        }

        #endregion

        private void checkDiskAndDeleteButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("checkDiskAndDeleteButton_Click enter");

            DialogResult answer = MessageBox.Show(
               "Are you sure you want delete any extra files on the backup disk not in our list?",
               "Delete extra files",
               MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                CheckConnectedDisk(true);
            }

            Utils.Trace("checkDiskAndDeleteButton_Click exit");
        }

        private void clearBackupDiskForFilesWithoutBackupDiskCheckedButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("clearBackupDiskForFilesWithoutBackupDiskCheckedButton_Click enter");

            IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesWithDiskCheckedEmpty();

            Utils.Log(logFile, "Clearing Disk for files without DiskChecked");

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    logFile,
                    $"{file.FullPath} does not have DiskChecked set on backup disk {file.Disk} so clearing the BackupDisk"
                    );

                file.Disk = null;
            }

            mediaBackup.Save();

            Utils.Trace("clearBackupDiskForFilesWithoutBackupDiskCheckedButton_Click exit");
        }

        private void timerButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("timerButton_Click enter");

            if (timerButton.Text == "Start timer")
            {
                timerButton.Text = "Stop timer";
                // Fire once if CheckBox is ticked
                if (runOnTimerStartCheckBox.Checked)
                {
                    ScheduledBackup();
                }

                trigger = new DailyTrigger(Convert.ToInt32(hoursNumericUpDown.Value), Convert.ToInt32(minutesNumericUpDown.Value));

                trigger.OnTimeTriggered += triggerAction;
            }
            else
            {
                timerButton.Text = "Start timer";
                trigger.OnTimeTriggered -= triggerAction;
            }

            Utils.Trace("timerButton_Click exit");
        }

        private void ScheduledBackup()
        {
            Utils.Trace("ScheduledBackup enter");

            try
            {
                // check the service monitor is running
                // Take a copy of the current count of files we backup up last time
                // Then ScanFolders
                // If the new file count is less than x% lower then abort
                // This happens if the server running the backup cannot connect to the nas devices sometimes
                // It'll then delete everything off the connected backup disk as it doesn't think they're needed so this will prevent that

                if (serviceMonitoringRunning)
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken,
                                        logFile, BackupAction.General,
                                        $"Service monitoring is running every {mediaBackup.MonitorInterval} seconds"
                                        );
                }
                else
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken,
                                        logFile, BackupAction.General, PushoverPriority.High,
                                        $"Service monitoring is not running"
                                        );
                }

                long oldFileCount = mediaBackup.BackupFiles.Count();

                // Update the master file
                ScanFolders();

                if (mediaBackup.DifferenceInFileCountAllowedPercentage != 0)
                {
                    long minimumFileCountAllowed = oldFileCount - (oldFileCount * mediaBackup.DifferenceInFileCountAllowedPercentage / 100);

                    long newFileCount = mediaBackup.BackupFiles.Count();

                    if (newFileCount < minimumFileCountAllowed)
                    {
                        throw new Exception("ERROR: The count of files to backup is too low. Check connections to nas drives");
                    }
                }

                // checks for backup disks not verified in > xx days
                CheckForOldBackupDisks();

                // Check the connected backup disk (removing any extra files we dont need)
                CheckConnectedDisk(true);

                // Copy any files that need a backup
                CopyFiles();
            }
            catch (Exception ex)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey, mediaBackup.PushoverAppToken,
                    logFile, BackupAction.General, PushoverPriority.Emergency,
                    $"Exception occured {ex}"
                    );
            }

            Utils.Trace("ScheduledBackup exit");
        }

        private void listFilesOnBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listFilesOnBackupDiskButton_Click enter");

            IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesOnBackupDisk(listFilesTextBox.Text);

            Utils.Log(logFile, $"Listing files on backup disk {listFilesTextBox.Text}");

            foreach (BackupFile file in files)
            {
                Utils.Log(logFile, $"{file.FullPath}");
            }

            Utils.Trace("listFilesOnBackupDiskButton_Click exit");

        }

        private void listFilesInMasterFolderButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listFilesInMasterFolderButton_Click enter");

            string masterFolder = masterFoldersComboBox.SelectedItem.ToString();

            IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder);

            Utils.Log(logFile, $"Listing files in master folder {masterFolder}");

            long readSpeed, writeSpeed;

            Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
            Utils.Log(logFile, $"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");

            foreach (BackupFile file in files)
            {
                Utils.Log(logFile, $"{file.FullPath} : {file.Disk}");
                if (string.IsNullOrEmpty(file.Disk))
                {
                    Utils.Log(logFile, $"ERROR: {file.FullPath} : not on a backup disk");
                }
            }

            Utils.Trace("listFilesInMasterFolderButton_Click exit");
        }

        private void CheckForOldBackupDisks_Click(object sender, EventArgs e)
        {
            Utils.Trace("CheckForOldBackupDisks_Click enter");

            CheckForOldBackupDisks();

            Utils.Trace("CheckForOldBackupDisks_Click exit");
        }

        private void CheckForOldBackupDisks()
        {
            Utils.Trace("CheckForOldBackupDisks enter");

            int numberOfDays = mediaBackup.DaysToReportOldBackupDisks;

            IEnumerable<BackupFile> files = mediaBackup.BackupFiles.Where(p => p.DiskChecked.HasValue() &&
                                            DateTime.Parse(p.DiskChecked).AddDays(numberOfDays) < DateTime.Today);

            IEnumerable<BackupFile> disks = files.GroupBy(p => p.Disk).Select(p => p.First());

            foreach (BackupFile disk in disks)
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                      mediaBackup.PushoverAppToken,
                                      logFile,
                                      BackupAction.CheckBackupDisk,
                                      $"Backup disks not checked in {numberOfDays} days - {disk.Disk}"
                                      );
            }

            if (files.Count() == 0)
            {
                Utils.Log(logFile, $"All files checked in last {mediaBackup.DaysToReportOldBackupDisks} days");

            }
            else
            {
                Utils.Log(logFile, $"Listing files not checked in {mediaBackup.DaysToReportOldBackupDisks} days");
            }

            foreach (BackupFile file in files)
            {
                int days = DateTime.Today.Subtract(DateTime.Parse(file.DiskChecked)).Days;

                Utils.Log(logFile, $"{file.FullPath} - not checked in {days} day(s) on disk {file.Disk}");
            }

            Utils.Trace("CheckForOldBackupDisks exit");
        }

        private void restoreFilesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("restoreFilesButton_Click enter");

            // loop through all the files looking for the master folder specified in the top drop down and copy to the bottom drop down 
            // for each file order by backup disk
            // prompt for the back up disk to be inserted 
            // check we have it inserted
            // copy any files off this disk until we're all done to the new disk that we specified

            DialogResult answer = MessageBox.Show(
               "Are you sure you want to copy files from multiple backup disks to the new master folder location?",
               "Restore backup files",
               MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                if (masterFoldersComboBox.SelectedItem == null)
                {
                    MessageBox.Show(
                    "You must select a master folder that you'd files the files from backup disks restored for. This is typically the drive that is now failing",
                    "Restore backup files",
                    MessageBoxButtons.OK);
                    return;
                }

                string masterFolder = masterFoldersComboBox.SelectedItem.ToString();

                if (restoreMasterFolderComboBox.SelectedItem == null)
                {
                    MessageBox.Show(
                    "You must select a master folder that you'd files the files from backup copied to. This is typically a new drive that will replace the failing drive",
                    "Restore backup files",
                    MessageBoxButtons.OK);
                    return;
                }
                string targetMasterFolder = restoreMasterFolderComboBox.SelectedItem.ToString();

                IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder).Where(p => p.Disk.HasValue());

                Utils.Log(logFile, $"Restoring files from master folder {masterFolder}");
                Utils.Log(logFile, $"Restoring files to target master folder {targetMasterFolder}");

                string backupShare = backupDiskTextBox.Text;

                string lastBackupDisk = string.Empty;
                int fileCounter = 0;
                int countOfFiles = 0;

                foreach (BackupFile file in files)
                {
                    if (!mediaBackup.DisksToSkipOnRestore.Contains(file.Disk, StringComparer.CurrentCultureIgnoreCase))
                    {
                        //we need to check the correct disk is connected and prompt if not
                        if (!EnsureConnectedBackupDisk(file.Disk, logFile))
                        {
                            MessageBox.Show("Cannot connect to the backup drive required", "Restore backup files", MessageBoxButtons.OK);
                            return;
                        }

                        if (file.Disk != lastBackupDisk)
                        {
                            if (!mediaBackup.DisksToSkipOnRestore.Contains(lastBackupDisk, StringComparer.CurrentCultureIgnoreCase) && lastBackupDisk.HasValue())
                            {
                                mediaBackup.DisksToSkipOnRestore.Add(lastBackupDisk);

                                // This is to save the backup disks we've completed so far
                                mediaBackup.Save();
                            }

                            // count the number of files on this disk
                            countOfFiles = files.Count(p => p.Disk == file.Disk);
                            fileCounter = 0;
                        }

                        fileCounter++;

                        // calculate the source path
                        // calculate the destination path
                        string sourceFileFullPath = Path.Combine(backupShare, file.Disk, file.IndexFolder, file.RelativePath);

                        string targetFilePath = Path.Combine(targetMasterFolder, file.IndexFolder, file.RelativePath);

                        if (File.Exists(targetFilePath))
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                  mediaBackup.PushoverAppToken,
                                                  logFile,
                                                  BackupAction.Restore,
                                                  $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists"
                                                  );
                        }
                        else
                        {
                            if (File.Exists(sourceFileFullPath))
                            {
                                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                      mediaBackup.PushoverAppToken,
                                                      logFile,
                                                      BackupAction.Restore,
                                                      $"[{fileCounter}/{countOfFiles}] Copying {sourceFileFullPath} as {targetFilePath}"
                                                      );
                                Utils.EnsureDirectories(targetFilePath);
                                File.Copy(sourceFileFullPath, targetFilePath);
                            }
                            else
                            {
                                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                      mediaBackup.PushoverAppToken,
                                                      logFile,
                                                      BackupAction.Restore,
                                                      PushoverPriority.High,
                                                      $"[{fileCounter}/{countOfFiles}] {sourceFileFullPath} doesn't exist"
                                                      );
                            }
                        }

                        if (File.Exists(targetFilePath))
                        {
                            if (file.ContentsHash == Utils.GetShortMd5HashFromFile(targetFilePath))
                            {
                                file.MasterFolder = targetMasterFolder;
                            }
                            else
                            {
                                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                                      mediaBackup.PushoverAppToken,
                                                      logFile,
                                                      BackupAction.Restore,
                                                      PushoverPriority.High,
                                                      $"ERROR: '{targetFilePath}' has a different Hashcode"
                                                      );
                            }
                        }
                    }

                    lastBackupDisk = file.Disk;
                }

                mediaBackup.Save();
            }

            Utils.Trace("restoreFilesButton_Click exit");
        }

        private void checkBackupDeleteAndCopyButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("checkBackupDeleteAndCopyButton_Click enter");

            DialogResult answer = MessageBox.Show(
               "Are you sure you want delete any extra files on the backup disk not in our list?",
               "Delete extra files",
               MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                CheckConnectedDisk(true);
                CopyFiles();
            }

            Utils.Trace("checkBackupDeleteAndCopyButton_Click exit");
        }

        private void listMoviesWithMultipleFilesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listMoviesWithMultipleFilesButton_Click enter");

            // listing movies with multiple files
            Utils.Log(logFile, "Listing movies with multiple files in folder");

            foreach (BackupFile file in mediaBackup.BackupFiles)
            {
                if (file.IndexFolder == "_Movies")
                {
                    string movieFolderName = file.RelativePath.Substring(0, file.RelativePath.IndexOf("\\"));

                    string movieFilename = file.RelativePath.Substring(file.RelativePath.IndexOf("\\") + 1);

                    if (movieFilename.StartsWith(movieFolderName) && movieFilename.Contains("{tmdb-") && movieFilename.EndsWith(".mkv"))
                    {
                        IEnumerable<BackupFile> otherFiles = mediaBackup.BackupFiles.Where(p => p.RelativePath.StartsWith(movieFolderName + "\\" + movieFolderName)
                        && p.RelativePath != file.RelativePath
                        && p.RelativePath.EndsWith(".mkv"));

                        foreach (BackupFile additionalFile in otherFiles)
                        {
                            Utils.Log(logFile, $"{additionalFile.FullPath}");
                        }
                    }
                }
            }

            Utils.Trace("listMoviesWithMultipleFilesButton_Click exit");
        }

        private void testPushoverHighButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverHighButton_Click enter");

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.General,
                                  PushoverPriority.High,
                                  "High priority test");

            Utils.Trace("testPushoverHighButton_Click exit");
        }

        private void testPushoverNormalButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverNormalButton_Click enter");

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.General,
                                  PushoverPriority.Normal,
                                  "Normal priority test\nLine 2\nLine 3");

            Utils.Trace("testPushoverNormalButton_Click exit");
        }

        private void testPushoverEmergencyButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverEmergencyButton_Click enter");

            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                  mediaBackup.PushoverAppToken,
                                  logFile,
                                  BackupAction.General,
                                  PushoverPriority.Emergency,
                                  PushoverRetry.OneMinute,
                                  PushoverExpires.OneHour,
                                  "Emergency priority test");

            Utils.Trace("testPushoverEmergencyButton_Click exit");
        }

        private void reportBackupDiskStatusButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("reportBackupDiskStatusButton_Click enter");

            IEnumerable<BackupDisk> disks = mediaBackup.BackupDisks.OrderBy(p => p.Number);

            Utils.Log(logFile, "Listing backup disk statuses");

            foreach (BackupDisk disk in disks)
            {
                DateTime d = DateTime.Parse(disk.Checked);
                Utils.Log(logFile, $"{disk.Name} at {disk.CapacityFormatted} with {disk.FreeFormatted} free. Last check: {d:dd-MMM-yy}");
            }

            string totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Capacity));
            string totalFreespaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Free));

            Utils.Log(logFile, $"Total available storage is {totalSizeFormatted} with {totalFreespaceFormatted} free");

            Utils.Trace("reportBackupDiskStatusButton_Click exit");
        }

        private void speedTestButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("speedTestButton_Click enter");

            long readSpeed, writeSpeed;

            Utils.Log(logFile, "Speed testing all master folders");

            foreach (string masterFolder in mediaBackup.MasterFolders)
            {
                if (Utils.IsFolderWritable(masterFolder))
                {
                    Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
                    Utils.Log(logFile, $"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
                }
            }

            Utils.Trace("speedTestButton_Click exit");
        }

        private void minutesNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Utils.Trace("minutesNumericUpDown_ValueChanged enter");

            if (minutesNumericUpDown.Value == 60)
            {
                minutesNumericUpDown.Value = 0;
            }

            if (minutesNumericUpDown.Value == -1)
            {
                minutesNumericUpDown.Value = 59;
            }

            mediaBackup.ScheduledBackupStartTime = $"{hoursNumericUpDown.Value}:{minutesNumericUpDown.Value}";

            Utils.Trace("minutesNumericUpDown_ValueChanged exit");
        }

        private void hoursNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Utils.Trace("hoursNumericUpDown_ValueChanged enter");

            if (hoursNumericUpDown.Value == 24)
            {
                hoursNumericUpDown.Value = 0;
            }

            if (hoursNumericUpDown.Value == -1)
            {
                hoursNumericUpDown.Value = 23;
            }

            mediaBackup.ScheduledBackupStartTime = $"{hoursNumericUpDown.Value}:{minutesNumericUpDown.Value}";

            Utils.Trace("hoursNumericUpDown_ValueChanged exit");
        }

        private void monitoringButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("monitoringButton_Click enter");

            if (serviceMonitoringRunning)
            {
                monitoringTimer.Stop();
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                              mediaBackup.PushoverAppToken,
                              logFile,
                              BackupAction.Monitoring, "Stopped"
                            );

                monitoringButton.Text = "Start monitoring";
                serviceMonitoringRunning = false;
            }
            else
            {
                Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                              mediaBackup.PushoverAppToken,
                              logFile,
                              BackupAction.Monitoring, "Started"
                            );

                monitoringTimer_Tick(null, null);

                monitoringTimer.Interval = mediaBackup.MonitorInterval * 1000;
                monitoringTimer.Start();
                monitoringButton.Text = "Stop monitoring";
                serviceMonitoringRunning = true;
            }

            Utils.Trace("monitoringButton_Click exit");
        }

        private void monitoringTimer_Tick(object sender, EventArgs e)
        {
            Utils.Trace("monitoringTimer_Tick enter");

            foreach (Monitor monitor in mediaBackup.Monitors)
            {
                bool result = monitor.Port > 0 ?
                    Utils.ConnectionExists(monitor.Url, monitor.Port) :
                    Utils.UrlExists(monitor.Url, monitor.Timeout * 1000);

                // The monitor is down
                if (!result)
                {
                    string text = $"'{monitor.Name}' is down";

                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                 mediaBackup.PushoverAppToken,
                                 logFile,
                                 BackupAction.Monitoring,
                                 PushoverPriority.High,
                                 text);

                    if (monitor.ProcessToKill.HasValue())
                    {
                        text = $"Stopping all '{monitor.ProcessToKill}' processes that match";

                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                                mediaBackup.PushoverAppToken,
                                logFile,
                                BackupAction.Monitoring,
                                PushoverPriority.Normal,
                                text);

                        Utils.KillProcesses(monitor.ProcessToKill);
                    }

                    if (monitor.ApplicationToStart.HasValue())
                    {
                        text = $"Starting {monitor.ApplicationToStart}";
 
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                              mediaBackup.PushoverAppToken,
                              logFile,
                              BackupAction.Monitoring,
                              PushoverPriority.Normal,
                              text);

                        string processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);
                        var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

                        if (newProcess == null)
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                             mediaBackup.PushoverAppToken,
                             logFile,
                             BackupAction.Monitoring,
                             PushoverPriority.High,
                             $"Failed to start the new process '{monitor.Name}'");
                        }
                        else
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                              mediaBackup.PushoverAppToken,
                              logFile,
                              BackupAction.Monitoring,
                              PushoverPriority.Normal,
                              $"'{monitor.Name}' started");
                        }
                    }

                    if (monitor.ServiceToRestart.HasValue())
                    {
                        text = $"Restarting '{monitor.ServiceToRestart}'";

                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                              mediaBackup.PushoverAppToken,
                              logFile,
                              BackupAction.Monitoring,
                              PushoverPriority.Normal,
                              text);

                        result = Utils.RestartService(monitor.ServiceToRestart, 5000);

                        if (result)
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                             mediaBackup.PushoverAppToken,
                             logFile,
                             BackupAction.Monitoring,
                             PushoverPriority.Normal,
                             $"'{monitor.Name}' started");
                        }
                        else
                        {
                            Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                            mediaBackup.PushoverAppToken,
                            logFile,
                            BackupAction.Monitoring,
                            PushoverPriority.High,
                            $"Failed to restart the service '{monitor.Name}'");
                        }
                    }
                }
            }

            Utils.Trace("monitoringTimer_Tick exit");
        }

        private void killProcessesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("killProcessesButton_Click enter");

            foreach (Monitor monitor in mediaBackup.Monitors)
            {
                if (monitor.ProcessToKill.HasValue())
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                            mediaBackup.PushoverAppToken,
                            logFile,
                            BackupAction.Monitoring,
                            PushoverPriority.Normal,
                            $"Stopping all '{monitor.ProcessToKill}' processes that match");

                    Utils.KillProcesses(monitor.ProcessToKill);
                }

                if (monitor.ServiceToRestart.HasValue())
                {
                    Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                          mediaBackup.PushoverAppToken,
                          logFile,
                          BackupAction.Monitoring,
                          PushoverPriority.Normal,
                          $"Stopping '{monitor.ServiceToRestart}'");

                    if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                    {
                        Utils.LogWithPushover(mediaBackup.PushoverUserKey,
                        mediaBackup.PushoverAppToken,
                        logFile,
                        BackupAction.Monitoring,
                        PushoverPriority.High,
                        $"Failed to stop the service '{monitor.Name}'");
                    }
                }
            }

            Utils.Trace("killProcessesButton_Click exit");
        }
    }
}