
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
    using System.Text.RegularExpressions;
    using static System.Net.WebRequestMethods;

    public partial class Main : Form
    {
        #region Fields

        private readonly MediaBackup mediaBackup;

        private const int DiskSpeedTestFileSize = 200 * Utils.BytesInOneMegabyte;
        private const int DiskSpeedTestIterations = 1;

        #endregion

        private DailyTrigger trigger;

        private Action scheduledBackupAction;

        private Action monitoringAction;

        // When the serice monitoring has been enabled this is True
        private bool serviceMonitoringRunning;

        #region Constructors and Destructors

        public Main()
        {
            InitializeComponent();

#if DEBUG
            Trace.Listeners.Add(new TextWriterTraceListener(
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManagerTrace.log"),
                         "myListener"));

            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "BackupMgrTest");
#else
            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");
#endif

            string mediaBackupXml = ConfigurationManager.AppSettings.Get("MediaBackupXml");

            string localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");

            mediaBackup = MediaBackup.Load(System.IO.File.Exists(localMediaXml) ? localMediaXml : mediaBackupXml);

            Utils.PushoverUserKey = mediaBackup.PushoverUserKey;
            Utils.PushoverAppToken = mediaBackup.PushoverAppToken;

            // Log the parameters after setting the Pushover keys in the Utils class
            mediaBackup.LogParameters();

            // Populate the MasterFolders combo boxes
            string[] masterFoldersArray = mediaBackup.MasterFolders.ToArray();

            listMasterFoldersComboBox.Items.AddRange(masterFoldersArray);
            masterFoldersComboBox.Items.AddRange(masterFoldersArray);
            restoreMasterFolderComboBox.Items.AddRange(masterFoldersArray);

            foreach (var disk in mediaBackup.BackupDisks)
            {
                listFilesComboBox.Items.Add(disk.Name);
            }

            foreach(var monitor in mediaBackup.Monitors)
            {
                processesComboBox.Items.Add(monitor.Name);
            }

            scheduledBackupAction = () => { ScheduledBackup(); };

            monitoringAction = () => { MonitorServices(); };

            DateTime startTime = DateTime.Parse(mediaBackup.ScheduledBackupStartTime);

            hoursNumericUpDown.Value = startTime.Hour;

            minutesNumericUpDown.Value = startTime.Minute;

            if (mediaBackup.StartMonitoring)
            {
#if !DEBUG
                monitoringButton_Click(null, null);
#endif
            }

            if (mediaBackup.StartScheduledBackup)
            {
#if !DEBUG
                timerButton_Click(null, null);
#endif
            }

            Utils.Trace("Main exit");
        }

        #endregion

        #region Methods

        private void ListFilesWithoutBackupDiskCheckedButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("ListFilesWithoutBackupDiskCheckedButton_Click enter");

            Utils.Log("Listing files without DiskChecked");

            foreach (BackupFile file in mediaBackup.GetBackupFilesWithDiskCheckedEmpty())
            {
                Utils.Log($"{file.FullPath} does not have DiskChecked set on disk {file.Disk}");
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

            Utils.LogWithPushover(BackupAction.CheckBackupDisk, "Started");

            BackupDisk disk = SetupBackupDisk();

            string folderToCheck = disk.BackupPath;

            long readSpeed = 0, writeSpeed = 0;
            if (mediaBackup.DiskSpeedTests)
            {
                Utils.DiskSpeedTest(folderToCheck, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
            }

            string text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nRead: {Utils.FormatSpeed(readSpeed)}\nWrite: {Utils.FormatSpeed(writeSpeed)}";

            bool diskInfoMessageWasTheLastSent = true;

            Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);

            // put the latest speed tests into the BackupDisk xml
            disk.LastReadSpeed = Utils.FormatSpeed(readSpeed);
            disk.LastWriteSpeed = Utils.FormatSpeed(writeSpeed);

            if (disk.Free < Utils.ConvertMBtoBytes(mediaBackup.MinimumCriticalBackupDiskSpace))
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                      PushoverPriority.High,
                                      $"{disk.FreeFormatted} free is very low. Prepare new backup disk");
            }

            IEnumerable<BackupFile> filesToReset = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name);

            foreach (BackupFile fileName in filesToReset)
            {
                fileName.ClearDiskChecked();
            }

            string[] backupDiskFiles = Utils.GetFiles(folderToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden);

            foreach (string backupFileFullPath in backupDiskFiles)
            {
                string backupFileIndexFolderRelativePath = backupFileFullPath.Substring(folderToCheck.Length + 1);

                if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
                {
                    BackupFile backupFile = mediaBackup.GetBackupFile(backupFileIndexFolderRelativePath);

                    // This happens when the file we have on the backup disk is no longer in the masterFolder
                    if (System.IO.File.Exists(backupFile.FullPath))
                    {
                        // This forces a hash check on the source and backup disk files
                        Utils.Trace($"Checking hash for {backupFile.Hash}");
                        bool returnValue = backupFile.CheckContentHashes(disk);

                        if (returnValue == false)
                        {
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                                  PushoverPriority.High,
                                                  $"There was an error with the hashcodes on the source and backup disk. It's likely the sourcefile has changed since the last backup of {backupFile.FullPath}. It could be that the source file or destination file are corrupted though.");

                            diskInfoMessageWasTheLastSent = false;

                        }
                        continue;
                    }
                }
                else
                {
                    // The file on the backup disk isn't found in the masterfolder anymore
                    // it could be that we've renamed it in the master folder
                    // We could just let it get deleted off the backup disk and copied again next time
                    // Alternatively, find it by the contents hashcode as thats (alost guaranteed unique)
                    // and then rename it 
                    var hashToCheck = Utils.GetShortMd5HashFromFile(backupFileFullPath);

                    var file = mediaBackup.GetBackupFileFromContentsHashcode(hashToCheck);

                    if (file != null && file.Length != 0 && file.BackupDiskNumber == 0)
                    {
                        string destFileName = file.BackupDiskFullPath(disk.BackupPath);
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.High, $"Renaming {backupFileFullPath} to {destFileName}");
                        Utils.FileMove(backupFileFullPath, destFileName);

                        // This forces a hash check on the source and backup disk files
                        Utils.Trace($"Checking hash for {file.Hash}");
                        bool returnValue = file.CheckContentHashes(disk);

                        if (returnValue == false)
                        {
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                                  PushoverPriority.High,
                                                  $"There was an error with the hashcodes on the source and backup disk. It's likely the sourcefile has changed since the last backup of {file.FullPath}. It could be that the source file or destination file are corrupted though.");

                            diskInfoMessageWasTheLastSent = false;
                        }
                        continue;
                    }
                }

                // Extra file on a backup disk
                if (deleteExtraFiles)
                {
                    Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                          $"Extra file {backupFileFullPath} on backup disk {disk.Name} now deleted");
                    Utils.FileDelete(backupFileFullPath);
                    diskInfoMessageWasTheLastSent = false;
                }
                else
                {
                    Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                          $"Extra file {backupFileFullPath} on backup disk {disk.Name}");
                    diskInfoMessageWasTheLastSent = false;
                }
            }

            string[] directoriesDeleted = Utils.DeleteEmptyDirectories(folderToCheck);

            foreach (string directory in directoriesDeleted)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Deleted empty folder {directory}");
            }

            disk.UpdateDiskChecked();

            bool result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}"
                                      );
                return;
            }

            mediaBackup.Save();

            if (!diskInfoMessageWasTheLastSent)
            {
                text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";

                Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
            }
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, "Completed");

            Utils.Trace("CheckConnectedDisk exit");
        }

        private bool EnsureConnectedBackupDisk(string backupDisk)
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
                Utils.LogWithPushover(BackupAction.General,
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

        private BackupDisk SetupBackupDisk()
        {
            // check the backupDisks has an entry for this disk
            BackupDisk disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
            if (disk == null)
            {
                Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup share {backupDiskTextBox.Text}"
                                      );
                return null;
            }

            if (disk.Update(mediaBackup.BackupFiles))
            {
                return disk;
            }
            else
            {
                Utils.LogWithPushover(BackupAction.BackupFiles,
                                          PushoverPriority.Emergency,
                                          $"Error updating info for backup {disk.Name}"
                                          );
                return null;
            }
        }

        private void CopyFiles()
        {
            Utils.Trace("CopyFiles enter");

            BackupDisk disk = SetupBackupDisk();

            Utils.LogWithPushover(BackupAction.BackupFiles, "Started");

            IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(q => q.Length);

            long sizeOfFiles = filesToBackup.Sum(x => x.Length);

            int totalFileCount = filesToBackup.Count();

            Utils.LogWithPushover(BackupAction.BackupFiles,
                                  $"{totalFileCount:n0} files to backup at {Utils.FormatSize(sizeOfFiles)}"
                                 );

            bool outOfDiskSpaceMessageSent = false;
            bool result = false;

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

                    string destinationFileName = backupFile.BackupDiskFullPath(disk.BackupPath);

                    // We use a temporary name for the copy first and then rename it after
                    // This is in case the Backup is aborted during the copy
                    // This file will be seen on the next scan and removed
                    string destinationFileNameTemp = destinationFileName + ".copying";

                    string sourceFileName = backupFile.FullPath;
                    FileInfo sourceFileInfo = new FileInfo(sourceFileName);
                    string sourceFileSize = Utils.FormatSize(sourceFileInfo.Length);

                    if (System.IO.File.Exists(destinationFileName))
                    {
                        Utils.Log(BackupAction.BackupFiles, $"[{fileCounter}/{totalFileCount}] Skipping copy as it exists. Checking hashes instead.");

                        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                        // in which case check the source hash again and then check the copied file 
                        bool returnValue = backupFile.CheckContentHashes(disk);

                        if (returnValue == false)
                        {
                            // There was an error with the hashcodes of the source file anf the file on the backup disk
                            Utils.LogWithPushover(BackupAction.BackupFiles,
                                                  PushoverPriority.High,
                                                  $"There was an error with the hashcodes on the source master folder and the backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath} to {destinationFileName}");
                        }
                    }
                    else
                    {
                        long availableSpace;
                        long totalBytes;
                        result = Utils.GetDiskInfo(backupDiskTextBox.Text, out availableSpace, out totalBytes);

                        if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk))
                        {
                            if (availableSpace > sourceFileInfo.Length)
                            {
                                outOfDiskSpaceMessageSent = false;
                                Utils.LogWithPushover(BackupAction.BackupFiles,
                                                      $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nCopying {sourceFileName} at {sourceFileSize}");

                                Utils.FileDelete(destinationFileNameTemp);

                                DateTime startTime = DateTime.UtcNow;
                                Utils.FileCopy(sourceFileName, destinationFileNameTemp);
                                DateTime endTime = DateTime.UtcNow;
                                Utils.FileMove(destinationFileNameTemp, destinationFileName);

                                double timeTaken = (endTime - startTime).TotalSeconds;
                                Utils.Trace($"timeTaken {timeTaken}");
                                Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");

                                string copySpeed = timeTaken > 0 ?
                                    Utils.FormatSpeed(Convert.ToInt64(sourceFileInfo.Length / timeTaken)) : "a very fast speed";

                                Utils.Trace($"Copy complete at {copySpeed}");

                                // Make sure its not readonly
                                Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                                // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                                // in which case check the source hash again and then check the copied file 
                                bool returnValue = backupFile.CheckContentHashes(disk);

                                if (returnValue == false)
                                {
                                    // There was an error with the hashcodes of the source file anf the file on the backup disk
                                    Utils.LogWithPushover(BackupAction.BackupFiles,
                                                          PushoverPriority.High,
                                                          $"There was an error with the hashcodes on the source and backup disk. Its likely the sourcefile has changed since the last backup of {backupFile.FullPath} to {destinationFileName}");
                                }
                            }
                            else
                            {
                                if (!outOfDiskSpaceMessageSent)
                                {
                                    Utils.LogWithPushover(BackupAction.BackupFiles,
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
                    Utils.LogWithPushover(BackupAction.BackupFiles,
                                          PushoverPriority.High,
                                          $"{backupFile.FullPath} is not found. It's most likely been replaced since our scan.");
                }

                catch (IOException ex)
                {
                    // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                    Utils.LogWithPushover(BackupAction.BackupFiles,
                                          PushoverPriority.Emergency,
                                          $"IOException during copy. Skipping file. Details {ex}");
                }
            }

            result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}");
                return;
            }

            mediaBackup.Save();

            IEnumerable<BackupFile> filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

            if (filesStillNotOnBackupDisk.Count() > 0)
            {
                sizeOfFiles = filesStillNotOnBackupDisk.Sum(p => p.Length);

                Utils.LogWithPushover(BackupAction.BackupFiles,
                                      $"{filesStillNotOnBackupDisk.Count():n0} files still to backup at {Utils.FormatSize(sizeOfFiles)}");
            }

            IEnumerable<BackupFile> filesWithoutDiskChecked = mediaBackup.GetBackupFilesWithDiskCheckedEmpty();

            if (filesWithoutDiskChecked.Count() > 0)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles,
                                      $"{filesWithoutDiskChecked.Count():n0} files still without DiskChecked set");
            }

            Utils.LogWithPushover(BackupAction.BackupFiles,
                                  $"{disk.FreeFormatted} free on backup disk");

            Utils.LogWithPushover(BackupAction.BackupFiles,
                                  PushoverPriority.High,
                                  "Completed");

            Utils.Trace("CopyFiles exit");
        }

        private void ListFilesNotOnBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("ListFilesNotOnBackupDiskButton_Click enter");

            IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(p => p.Length);

            Utils.Log("Listing files not on a backup disk");
            foreach (BackupFile file in filesNotOnBackupDisk)
            {
                Utils.Log($"{file.FullPath} at {Utils.FormatSize(file.Length)}");
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

            DialogResult answer = MessageBox.Show("Are you sure you want to rebuild the master list?",
                                                  "Rebuild master list",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                ScanFolders();
            }

            Utils.Trace("UpdateMasterFilesButtonClick exit");
        }
        /// <summary>
        /// Returns True if the file was deleted
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private bool CheckForFilesToDelete(string filePath)
        {
            IEnumerable<string> filters = from filter in mediaBackup.FilesToDelete
                                          let replace =
                                              filter.Replace("!", string.Empty)
                                              .Replace(".", @"\.")
                                              .Replace("*", ".*")
                                              .Replace("?", ".")
                                          select $"^{replace}$";

            string fileName = new FileInfo(filePath).Name;

            if (filters.Any(pattern => Regex.IsMatch(fileName, pattern)))
            {
                Utils.LogWithPushover(BackupAction.ScanFolders,
                                         PushoverPriority.Normal,
                                         $"File matches RegEx and so will be deleted {filePath}");
                Utils.FileDelete(filePath);
                return true;
            }

            return false;
        }

        private void ScanFolders()
        {
            Utils.Trace("ScanFolders enter");

            string filters = string.Join(",", mediaBackup.Filters.ToArray());

            long readSpeed =0, writeSpeed = 0;

            mediaBackup.ClearFlags();

            Utils.LogWithPushover(BackupAction.ScanFolders, "Started");

            foreach (string masterFolder in mediaBackup.MasterFolders)
            {
                if (!Directory.Exists(masterFolder))
                {
                    Utils.LogWithPushover(BackupAction.ScanFolders,
                                          PushoverPriority.High,
                                          $"{masterFolder} is not available");
                }

                if (Directory.Exists(masterFolder))
                {
                    if (!Utils.IsFolderWritable(masterFolder))
                    {
                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                              PushoverPriority.High,
                                              $"{masterFolder} is not writeable");
                    }

                    long freeSpaceOnCurrentMasterFolder;
                    long totalBytesOnMasterFolderDisk;
                    Utils.GetDiskInfo(masterFolder, out freeSpaceOnCurrentMasterFolder, out totalBytesOnMasterFolderDisk);

                    if (mediaBackup.DiskSpeedTests)
                    {
                        Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
                    }
                    string totalBytesOnMasterFolderDiskFormatted = Utils.FormatSize(totalBytesOnMasterFolderDisk);
                    string freeSpaceOnCurrentMasterFolderFormatted = Utils.FormatSize(freeSpaceOnCurrentMasterFolder);
                    string readSpeedFormatted = Utils.FormatSpeed(readSpeed);
                    string writeSpeedFormatted = Utils.FormatSpeed(writeSpeed);

                    string text = $"{masterFolder}\nTotal: {totalBytesOnMasterFolderDiskFormatted}\nFree: {freeSpaceOnCurrentMasterFolderFormatted}\nRead: {readSpeedFormatted}\nWrite: {writeSpeedFormatted}";

                    Utils.LogWithPushover(BackupAction.ScanFolders, text);

                    if (mediaBackup.DiskSpeedTests)
                    {
                        if (readSpeed < Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderReadSpeed))
                        {
                            Utils.LogWithPushover(BackupAction.ScanFolders,
                                                  PushoverPriority.High,
                                                  $"Read speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderReadSpeed))}");
                        }

                        if (writeSpeed < Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderWriteSpeed))
                        {
                            Utils.LogWithPushover(BackupAction.ScanFolders,
                                                  PushoverPriority.High,
                                                  $"Write speed is below MinimumCritical of {Utils.FormatSpeed(Utils.ConvertMBtoBytes(mediaBackup.MinimumMasterFolderWriteSpeed))}");
                        }
                    }
                    if (freeSpaceOnCurrentMasterFolder < Utils.ConvertMBtoBytes(mediaBackup.MinimumCriticalMasterFolderSpace))
                    {
                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                              PushoverPriority.High,
                                              $"Free space on {masterFolder} is too low");
                    }

                    foreach (string indexFolder in mediaBackup.IndexFolders)
                    {
                        string folderToCheck = Path.Combine(masterFolder, indexFolder);

                        if (Directory.Exists(folderToCheck))
                        {
                            Utils.LogWithPushover(BackupAction.ScanFolders, $"{folderToCheck}");

                            string[] files = Utils.GetFiles(folderToCheck, filters, SearchOption.AllDirectories);

                            foreach (string file in files)
                            {
                                Utils.Trace($"Checking {file}");

                                // Check for files to delete
                                if (CheckForFilesToDelete(file))
                                {
                                    continue;
                                }

                                // Checks for TV only
                                if (file.Contains("_TV"))
                                {
                                    if (!file.Contains("tvdb") && !file.Contains("tmdb"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
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
                                                Utils.LogWithPushover(BackupAction.ScanFolders,
                                                                      PushoverPriority.High,
                                                                      $"TV File {file} has files in the grandparent folder which aren't special features"
                                                                      );
                                            }
                                        }
                                    }

                                    // check the filename doesn't have TBA in it
                                    if (file.Contains("TBA"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"TV File {file} has TBA in the filename");
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
                                            Utils.LogWithPushover(BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"Movie has missing tmdb- in the filename {file}");
                                        }

                                        FileInfo movieFileInfo = new FileInfo(file);
                                        if (!movieFileInfo.Name.StartsWith(movieFileInfo.Directory.Name))
                                        {
                                            Utils.LogWithPushover(BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"Movie filename doesn't start with the folder name in the filename {file}");
                                        }
                                    }

                                    //Edition checks '{edition-EXTENDED EDITION}'
                                    if (file.Contains("{edition-"))
                                    {
                                        bool found = false;
                                        foreach (string s in mediaBackup.EditionsAllowed)
                                        {
                                            if (file.Contains("{edition-" + s + "}", StringComparison.OrdinalIgnoreCase))
                                            {
                                                found = true;
                                                break;
                                            }
                                        }
                                        if (!found)
                                        {
                                            Utils.LogWithPushover(BackupAction.ScanFolders,
                                                                  PushoverPriority.High,
                                                                  $"File has 'edition-' in the filename {file} but no valid edition specification");
                                        }
                                    }

                                    // check that movies only have files in the directories and no subfolders
                                    FileInfo fileInfo = new FileInfo(file);

                                    DirectoryInfo[] siblingDirectories = fileInfo.Directory.GetDirectories();
                                    if (siblingDirectories != null && siblingDirectories.Count() > 0)
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"File {file} has folders alongside it");
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
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has 'subtitles' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" ()"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has a missing year in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" (0)"))
                                    {
                                        Utils.LogWithPushover(
                                                              BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has a '0' year in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" Proper]"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has ' Proper]' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" Proper REAL]"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has ' Proper REAL]' in the filename {file}"
                                                              );
                                    }

                                    if (file.Contains(" REAL]"))
                                    {
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
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
                                        Utils.LogWithPushover(BackupAction.ScanFolders,
                                                              PushoverPriority.High,
                                                              $"Video has an invalid file extension in the filename {file}");
                                    }
                                }

                                // All files except Backup
                                if (!file.Contains("_Backup"))
                                {
                                    // Placeholder for file scans
                                }

                                mediaBackup.EnsureFile(file, masterFolder, indexFolder);
                            }
                        }
                    }
                }
                else
                {
                    Utils.LogWithPushover(BackupAction.ScanFolders,
                                          PushoverPriority.High,
                                          $"{masterFolder} doesn't exist");
                }
            }

            mediaBackup.RemoveFilesWithFlag(false, true);
            mediaBackup.Save();

            int totalFiles = mediaBackup.BackupFiles.Count();

            long totalFileSize = mediaBackup.BackupFiles.Sum(p => p.Length);

            IEnumerable<BackupFile> filesNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

            long fileSizeToCopy = filesNotOnBackupDisk.Sum(p => p.Length);

            Utils.LogWithPushover(BackupAction.ScanFolders,
                                  $"{totalFiles:n0} files at {Utils.FormatSize(totalFileSize)}");

            BackupFile oldestFile = mediaBackup.GetOldestFile();

            if (oldestFile != null)
            {
                DateTime oldestFileDate = DateTime.Parse(oldestFile.DiskChecked);

                Utils.LogWithPushover(BackupAction.ScanFolders,
                                      $"Oldest backup date is {DateTime.Today.Subtract(oldestFileDate).Days:n0} day(s) ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
            }

            Utils.LogWithPushover(BackupAction.ScanFolders,
                                  $"{filesNotOnBackupDisk.Count():n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");

            Utils.LogWithPushover(BackupAction.ScanFolders, "Completed");

            Utils.Trace("ScanFolders exit");
        }

        #endregion

        private void checkDiskAndDeleteButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("checkDiskAndDeleteButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
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

            Utils.Log("Clearing Disk for files without DiskChecked");

            foreach (BackupFile file in files)
            {
                Utils.Log($"{file.FullPath} does not have DiskChecked set on backup disk {file.Disk} so clearing the BackupDisk");
                file.Disk = null;
            }

            mediaBackup.Save();

            Utils.Trace("clearBackupDiskForFilesWithoutBackupDiskCheckedButton_Click exit");
        }

        private void timerButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("timerButton_Click enter");

            if (timerButton.Text == "Start")
            {
                timerButton.Text = "Stop";

                // Fire once if CheckBox is ticked
                if (runOnTimerStartCheckBox.Checked)
                {
                    scheduledBackupAction.BeginInvoke(scheduledBackupAction.EndInvoke, null);
                }

                trigger = new DailyTrigger(Convert.ToInt32(hoursNumericUpDown.Value), Convert.ToInt32(minutesNumericUpDown.Value));

                trigger.OnTimeTriggered += scheduledBackupAction;
            }
            else
            {
                timerButton.Text = "Start";
                trigger.OnTimeTriggered -= scheduledBackupAction;
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
                    Utils.LogWithPushover(BackupAction.General,
                                          $"Service monitoring is running every {mediaBackup.MonitorInterval} seconds");
                }
                else
                {
                    Utils.LogWithPushover(BackupAction.General,
                                          PushoverPriority.High,
                                          $"Service monitoring is not running");
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
                Utils.LogWithPushover(BackupAction.General,
                                      PushoverPriority.Emergency,
                                      $"Exception occured {ex}");
            }

            Utils.Trace("ScheduledBackup exit");
        }

        private void listFilesOnBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listFilesOnBackupDiskButton_Click enter");

            if (listFilesComboBox.SelectedItem != null)
            {
                string selectedItemText = listFilesComboBox.SelectedItem.ToString();

                IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesOnBackupDisk(selectedItemText);

                Utils.Log($"Listing files on backup disk {selectedItemText}");

                foreach (BackupFile file in files)
                {
                    Utils.Log($"{file.FullPath}");
                }
            }
            Utils.Trace("listFilesOnBackupDiskButton_Click exit");
        }

        private void listFilesInMasterFolderButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listFilesInMasterFolderButton_Click enter");

            string masterFolder = listMasterFoldersComboBox.SelectedItem.ToString();

            IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder);

            Utils.Log($"Listing files in master folder {masterFolder}");

            long readSpeed = 0, writeSpeed = 0;
            if (mediaBackup.DiskSpeedTests)
            {
                Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
            }

            Utils.Log($"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");

            foreach (BackupFile file in files)
            {
                Utils.Log($"{file.FullPath} : {file.Disk}");
                if (string.IsNullOrEmpty(file.Disk))
                {
                    Utils.Log($"ERROR: {file.FullPath} : not on a backup disk");
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
                Utils.LogWithPushover(BackupAction.CheckBackupDisk,
                                      $"Backup disks not checked in {numberOfDays} days - {disk.Disk}");
            }

            if (files.Count() == 0)
            {
                Utils.Log(BackupAction.General, $"All files checked in last {mediaBackup.DaysToReportOldBackupDisks} days");

            }
            else
            {
                Utils.Log(BackupAction.General, $"Listing files not checked in {mediaBackup.DaysToReportOldBackupDisks} days");
            }

            foreach (BackupFile file in files)
            {
                int days = DateTime.Today.Subtract(DateTime.Parse(file.DiskChecked)).Days;

                Utils.Log(BackupAction.General, $"{file.FullPath} - not checked in {days} day(s) on disk {file.Disk}");
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

            DialogResult answer = MessageBox.Show("Are you sure you want to copy files from multiple backup disks to the new master folder location?",
                                                  "Restore backup files",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                if (masterFoldersComboBox.SelectedItem == null)
                {
                    MessageBox.Show("You must select a master folder that you'd files the files from backup disks restored for. This is typically the drive that is now failing",
                                    "Restore backup files",
                                    MessageBoxButtons.OK);
                    return;
                }

                string masterFolder = masterFoldersComboBox.SelectedItem.ToString();

                if (restoreMasterFolderComboBox.SelectedItem == null)
                {
                    MessageBox.Show("You must select a master folder that you'd files the files from backup copied to. This is typically a new drive that will replace the failing drive",
                                    "Restore backup files",
                                    MessageBoxButtons.OK);
                    return;
                }

                string targetMasterFolder = restoreMasterFolderComboBox.SelectedItem.ToString();

                IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder).Where(p => p.Disk.HasValue());

                Utils.Log(BackupAction.Restore, $"Restoring files from master folder {masterFolder}");
                Utils.Log(BackupAction.Restore, $"Restoring files to target master folder {targetMasterFolder}");

                string backupShare = backupDiskTextBox.Text;

                string lastBackupDisk = string.Empty;
                int fileCounter = 0;
                int countOfFiles = 0;

                foreach (BackupFile file in files)
                {
                    if (!mediaBackup.DisksToSkipOnRestore.Contains(file.Disk, StringComparer.CurrentCultureIgnoreCase))
                    {
                        //we need to check the correct disk is connected and prompt if not
                        if (!EnsureConnectedBackupDisk(file.Disk))
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

                        if (System.IO.File.Exists(targetFilePath))
                        {
                            Utils.LogWithPushover(BackupAction.Restore,
                                                  $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists");
                        }
                        else
                        {
                            if (System.IO.File.Exists(sourceFileFullPath))
                            {
                                Utils.LogWithPushover(BackupAction.Restore,
                                                      $"[{fileCounter}/{countOfFiles}] Copying {sourceFileFullPath} as {targetFilePath}");
                                Utils.FileCopy(sourceFileFullPath, targetFilePath);
                            }
                            else
                            {
                                Utils.LogWithPushover(BackupAction.Restore,
                                                      PushoverPriority.High,
                                                      $"[{fileCounter}/{countOfFiles}] {sourceFileFullPath} doesn't exist");
                            }
                        }

                        if (System.IO.File.Exists(targetFilePath))
                        {
                            if (file.ContentsHash == Utils.GetShortMd5HashFromFile(targetFilePath))
                            {
                                file.MasterFolder = targetMasterFolder;
                            }
                            else
                            {
                                Utils.LogWithPushover(BackupAction.Restore,
                                                      PushoverPriority.High,
                                                      $"ERROR: '{targetFilePath}' has a different Hashcode");
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

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
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
            Utils.Log("Listing movies with multiple files in folder");

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
                            Utils.Log($"{additionalFile.FullPath}");
                        }
                    }
                }
            }

            Utils.Trace("listMoviesWithMultipleFilesButton_Click exit");
        }

        private void testPushoverHighButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverHighButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.High,
                                  "High priority test");

            Utils.Trace("testPushoverHighButton_Click exit");
        }

        private void testPushoverNormalButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverNormalButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.Normal,
                                  "Normal priority test\nLine 2\nLine 3");

            Utils.Trace("testPushoverNormalButton_Click exit");
        }

        private void testPushoverEmergencyButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverEmergencyButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
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

            Utils.Log("Listing backup disk statuses");

            foreach (BackupDisk disk in disks)
            {
                DateTime d = DateTime.Parse(disk.Checked);
                Utils.Log($"{disk.Name} at {disk.CapacityFormatted} with {disk.FreeFormatted} free. Last check: {d:dd-MMM-yy}");
            }

            string totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Capacity));
            string totalFreespaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Free));

            Utils.Log($"Total available storage is {totalSizeFormatted} with {totalFreespaceFormatted} free");

            Utils.Trace("reportBackupDiskStatusButton_Click exit");
        }

        private void speedTestButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("speedTestButton_Click enter");
            Utils.Log("Speed testing all master folders");

            foreach (string masterFolder in mediaBackup.MasterFolders)
            {
                if (Utils.IsFolderWritable(masterFolder))
                {
                    long readSpeed;
                    long writeSpeed;
                    Utils.DiskSpeedTest(masterFolder, DiskSpeedTestFileSize, DiskSpeedTestIterations, out readSpeed, out writeSpeed);
                    Utils.Log($"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
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
                Utils.LogWithPushover(BackupAction.Monitoring, "Stopped");

                monitoringButton.Text = "Start monitoring";
                serviceMonitoringRunning = false;
            }
            else
            {
                Utils.LogWithPushover(BackupAction.Monitoring, "Started");

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
            monitoringAction.BeginInvoke(monitoringAction.EndInvoke, null);
        }

        private void MonitorServices()
        {
            Utils.Trace("MonitorServices enter");

            foreach (Monitor monitor in mediaBackup.Monitors)
            {
                bool result;
                if (monitor.Port > 0)
                {
                    result = Utils.ConnectionExists(monitor.Url, monitor.Port);
                }
                else
                {
                    result = Utils.UrlExists(monitor.Url, monitor.Timeout * 1000);
                }

                // The monitor is down
                if (!result)
                {
                    string text = $"'{monitor.Name}' is down";

                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.High,
                                          text);

                    if (monitor.ProcessToKill.HasValue())
                    {
                        text = $"Stopping all '{monitor.ProcessToKill}' processes that match";

                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.Normal,
                                              text);

                        Utils.KillProcesses(monitor.ProcessToKill);
                    }

                    if (monitor.ApplicationToStart.HasValue())
                    {
                        text = $"Starting {monitor.ApplicationToStart}";

                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.Normal,
                                              text);

                        string processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

                        if (System.IO.File.Exists(processToStart))
                        {
                            var newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

                            if (newProcess == null)
                            {
                                Utils.LogWithPushover(BackupAction.Monitoring,
                                                      PushoverPriority.High,
                                                      $"Failed to start the new process '{monitor.Name}'");
                            }
                            else
                            {
                                Utils.LogWithPushover(BackupAction.Monitoring,
                                                      PushoverPriority.Normal,
                                                      $"'{monitor.Name}' started");
                            }
                        }
                        else
                        {
                            Utils.LogWithPushover(BackupAction.Monitoring,
                                                  PushoverPriority.High,
                                                  $"Failed to start the new process '{monitor.Name}' as its not found at {monitor.ApplicationToStart} (expanded to {processToStart})");
                        }
                    }

                    if (monitor.ServiceToRestart.HasValue())
                    {
                        text = $"Restarting '{monitor.ServiceToRestart}'";

                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.Normal,
                                              text);

                        result = Utils.RestartService(monitor.ServiceToRestart, monitor.Timeout * 1000);

                        if (result)
                        {
                            Utils.LogWithPushover(BackupAction.Monitoring,
                                                  PushoverPriority.Normal,
                                                  $"'{monitor.Name}' started");
                        }
                        else
                        {
                            Utils.LogWithPushover(BackupAction.Monitoring,
                                                  PushoverPriority.High,
                                                  $"Failed to restart the service '{monitor.Name}'");
                        }
                    }
                }
            }

            Utils.Trace("MonitorServices exit");
        }

        private void killProcessesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("killProcessesButton_Click enter");

            foreach (Monitor monitor in mediaBackup.Monitors)
            {
                if (monitor.ProcessToKill.HasValue())
                {
                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.Normal,
                                          $"Stopping all '{monitor.ProcessToKill}' processes that match");

                    Utils.KillProcesses(monitor.ProcessToKill);
                }

                if (monitor.ServiceToRestart.HasValue())
                {
                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.Normal,
                                          $"Stopping '{monitor.ServiceToRestart}'");

                    if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                    {
                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.High,
                                              $"Failed to stop the service '{monitor.Name}'");
                    }
                }
            }

            Utils.Trace("killProcessesButton_Click exit");
        }

        private void testPushoverLowButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverLowButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.Low,
                                  "Low priority test\nLine 2\nLine 3");

            Utils.Trace("testPushoverLowButton_Click exit");
        }

        private void Main_FormClosed(object sender, FormClosedEventArgs e)
        {
            Utils.Trace("Main_FormClosed enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.High,
                                  "BackupManager stopped");

            Utils.Trace("Main_FormClosed exit");
        }

        private void stopProcessButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("stopProcessButton_Click enter");

            if (processesComboBox.SelectedItem != null)
            {
                string monitorName = processesComboBox.SelectedItem.ToString();

                Monitor monitor = mediaBackup.Monitors.Where(m => m.Name == monitorName).First();

                if (monitor.ProcessToKill.HasValue())
                {
                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.Normal,
                                          $"Stopping all '{monitor.ProcessToKill}' processes that match");

                    Utils.KillProcesses(monitor.ProcessToKill);
                }

                if (monitor.ServiceToRestart.HasValue())
                {
                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.Normal,
                                          $"Stopping '{monitor.ServiceToRestart}'");

                    if (!Utils.StopService(monitor.ServiceToRestart, 5000))
                    {
                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.High,
                                              $"Failed to stop the service '{monitor.Name}'");
                    }
                }
            }

            Utils.Trace("stopProcessButton_Click exit");
        }
    }
}