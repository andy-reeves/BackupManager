// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Main.cs" company="Andy Reeves">
//
//  </copyright>
//  --------------------------------------------------------------------------------------------------------------------

namespace BackupManager
{
    using BackupManager.Entities;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Forms;

    public partial class Main : Form
    {
        #region Fields

        private CancellationTokenSource tokenSource = new CancellationTokenSource();

        private CancellationToken ct;

        private readonly MediaBackup mediaBackup;

        #endregion

        private DailyTrigger trigger;

        private readonly Action scheduledBackupAction;

        private readonly Action monitoringAction;

        #region Constructors and Destructors

        public Main()
        {
            InitializeComponent();
#if DEBUG
            Trace.Listeners.Add(new TextWriterTraceListener(
                         Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "BackupManager_Trace.log"),
                         "myListener"));

            backupDiskTextBox.Text = "\\\\nas1\\assets1\\_Test\\BackupDisks\\backup 1001 parent";
#else
            backupDiskTextBox.Text = Path.Combine(@"\\", Environment.MachineName, "backup");
#endif

            string mediaBackupXml = ConfigurationManager.AppSettings.Get("MediaBackupXml");

            string localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");

            mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : mediaBackupXml);

            Utils.PushoverUserKey = mediaBackup.PushoverUserKey;
            Utils.PushoverAppToken = mediaBackup.PushoverAppToken;
            Utils.MediaBackup = mediaBackup;

            // Log the parameters after setting the Pushover keys in the Utils class
            mediaBackup.LogParameters();

            // Populate the MasterFolders combo boxes
            string[] masterFoldersArray = mediaBackup.MasterFolders.ToArray();

            listMasterFoldersComboBox.Items.AddRange(masterFoldersArray);
            masterFoldersComboBox.Items.AddRange(masterFoldersArray);
            restoreMasterFolderComboBox.Items.AddRange(masterFoldersArray);

            foreach (BackupDisk disk in mediaBackup.BackupDisks)
            {
                listFilesComboBox.Items.Add(disk.Name);
            }

            foreach (ProcessServiceMonitor monitor in mediaBackup.Monitors)
            {
                processesComboBox.Items.Add(monitor.Name);
            }

            scheduledBackupAction = () => { TaskWrapper(ScheduledBackupAsync); };

            monitoringAction = () => { MonitorServices(); };

            DateTime startTime = DateTime.Parse(mediaBackup.ScheduledBackupStartTime);

            hoursNumericUpDown.Value = startTime.Hour;

            minutesNumericUpDown.Value = startTime.Minute;

            UpdateSendingPushoverButton();
            UpdateMonitoringButton();
            UpdateScheduledBackupButton();

            if (mediaBackup.StartMonitoring)
            {
#if !DEBUG
                MonitoringButton_Click(null, null);
#endif
            }

            if (mediaBackup.StartScheduledBackup)
            {
#if !DEBUG
                BackupTimerButton_Click(null, null);
#endif
            }

            Utils.Trace("Main exit");
        }

        #endregion

        #region Methods

        private void CheckConnectedBackupDiskButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("CheckConnectedBackupDriveButton_Click enter");

            TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, false, false);

            Utils.Trace("CheckConnectedBackupDriveButton_Click exit");
        }

        /// <summary>
        /// Returns a BackupDisk of the connected disk thats just been checked 
        /// </summary>
        /// <param name="deleteExtraFiles"></param>
        /// <returns>null if there was an error</returns>
        private BackupDisk CheckConnectedDisk(bool deleteExtraFiles)
        {
            Utils.Trace("CheckConnectedDisk enter");

            // Scans the connected backup disk and finds all its files
            // for each for found calculate the hash from the backup disk
            // find that hash in the backup data file
            // rebuilds the source filename from MasterFolder+IndexFolder+Path
            // checks the file still exists there
            // if it does compare the hashcodes and update results
            // force a recalc of both the hashes to check the files can both be read correctly

            BackupDisk disk = SetupBackupDisk();

            string folderToCheck = disk.BackupPath;
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, $"Started checking backup disk {folderToCheck}");
            UpdateStatusLabel($"Checking backup disk {folderToCheck}");

            long readSpeed = 0, writeSpeed = 0;

            if (mediaBackup.DiskSpeedTests)
            {
                long diskTestSize = disk.Free > Utils.ConvertMBtoBytes(mediaBackup.SpeedTestFileSize)
                                   ? Utils.ConvertMBtoBytes(mediaBackup.SpeedTestFileSize)
                                   : disk.Free - Utils.BytesInOneKilobyte;

                UpdateStatusLabel($"Speed testing {folderToCheck}");
                Utils.DiskSpeedTest(folderToCheck, diskTestSize, mediaBackup.SpeedTestIterations, out readSpeed, out writeSpeed);
            }

            string text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\n" +
                          $"Read: {Utils.FormatSpeed(readSpeed)}\nWrite: {Utils.FormatSpeed(writeSpeed)}";

            bool diskInfoMessageWasTheLastSent = true;

            Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);

            // put the latest speed tests into the BackupDisk xml
            if (readSpeed > 0)
            {
                disk.LastReadSpeed = Utils.FormatSpeed(readSpeed);
            }
            if (writeSpeed > 0)
            {
                disk.LastWriteSpeed = Utils.FormatSpeed(writeSpeed);
            }

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

            UpdateStatusLabel($"Scanning {folderToCheck}");

            string[] backupDiskFiles = Utils.GetFiles(folderToCheck, "*", SearchOption.AllDirectories, FileAttributes.Hidden);

            EnableProgressBar(0, backupDiskFiles.Length);

            for (int i = 0; i < backupDiskFiles.Length; i++)
            {
                string backupFileFullPath = backupDiskFiles[i];
                string backupFileIndexFolderRelativePath = backupFileFullPath.Substring(folderToCheck.Length + 1);

                UpdateStatusLabel($"Scanning {folderToCheck}", i + 1);

                if (mediaBackup.Contains(backupFileIndexFolderRelativePath))
                {
                    BackupFile backupFile = mediaBackup.GetBackupFile(backupFileIndexFolderRelativePath);

                    if (File.Exists(backupFile.FullPath))
                    {
                        // sometimes we get the same file on multiple backup disks
                        // calling CheckContentHashes will switch it from one disk to another and they'll keep doing it
                        // so if it was last seen on another disk delete it from this one

                        if (disk.Name != backupFile.Disk && backupFile.Disk.HasValue())
                        {
                            Utils.Log($"{backupFile.FullPath} was on {backupFile.Disk} but now found on {disk.Name}");
                            // we will fall through from here to the delete further down and remove the file
                        }

                        else
                        {
                            // This forces a hash check on the source and backup disk files
                            Utils.Trace($"Checking hash for {backupFile.Hash}");
                            bool returnValue = backupFile.CheckContentHashes(disk);

                            if (!returnValue)
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
                }
                else
                {
                    // The file on the backup disk isn't found in the masterfolder anymore
                    // it could be that we've renamed it in the master folder
                    // We could just let it get deleted off the backup disk and copied again next time
                    // Alternatively, find it by the contents hashcode as thats (alost guaranteed unique)
                    // and then rename it 
                    // if we try to rename and it exists at the destination already then we delete the file instead
                    string hashToCheck = Utils.GetShortMd5HashFromFile(backupFileFullPath);

                    BackupFile file = mediaBackup.GetBackupFileFromContentsHashcode(hashToCheck);

                    if (file != null && file.Length != 0 && file.BackupDiskNumber == 0)
                    {
                        string destFileName = file.BackupDiskFullPath(disk.BackupPath);
                        Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal, $"Renaming {backupFileFullPath} to {destFileName}");

                        if (File.Exists(destFileName))
                        {
                            // check the hash of the destination file to check its the same as what we would've renamed too
                            if (Utils.GetShortMd5HashFromFile(destFileName) == hashToCheck)
                            {
                                Utils.LogWithPushover(BackupAction.CheckBackupDisk, PushoverPriority.Normal, $"File exists already so deleting {backupFileFullPath} instead");
                                Utils.FileDelete(backupFileFullPath);
                            }
                        }
                        else
                        {
                            Utils.FileMove(backupFileFullPath, destFileName);
                        }

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

            UpdateStatusLabel($"Deleting {folderToCheck} empty folders");

            string[] directoriesDeleted = Utils.DeleteEmptyDirectories(folderToCheck);

            foreach (string directory in directoriesDeleted)
            {
                Utils.Log(BackupAction.CheckBackupDisk, $"Deleted empty folder {directory}");
            }

            disk.UpdateDiskChecked();

            bool result = disk.Update(mediaBackup.BackupFiles);
            if (!result)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles,
                                      PushoverPriority.Emergency,
                                      $"Error updating info for backup disk {disk.Name}"
                                      );
                return null;
            }

            mediaBackup.Save();
            UpdateStatusLabel($"Saved.");

            if (!diskInfoMessageWasTheLastSent)
            {
                text = $"Name: {disk.Name}\nTotal: {disk.CapacityFormatted}\nFree: {disk.FreeFormatted}\nFiles: {disk.TotalFiles:n0}";

                Utils.LogWithPushover(BackupAction.CheckBackupDisk, text);
            }
            Utils.LogWithPushover(BackupAction.CheckBackupDisk, "Completed");

            Utils.Trace("CheckConnectedDisk exit");

            return disk;
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

            TaskWrapper(CopyFilesAsync, true);

            Utils.Trace("CopyFilesToBackupDiskButton_Click exit");
        }

        /// <summary>
        /// Waits for a valid backup disk to be inserted
        /// </summary>
        /// <returns></returns>
        private BackupDisk SetupBackupDisk()
        {
            string nextDiskMessage = "Please insert the next backup disk now";

            BackupDisk disk;
            do
            {
                // check the backupDisks has an entry for this disk
                disk = mediaBackup.GetBackupDisk(backupDiskTextBox.Text);
                if (disk != null)
                {
                    return disk.Update(mediaBackup.BackupFiles) ? disk : null;
                }
                WaitForNewDisk(nextDiskMessage);
            } while (disk == null);

            return disk;
        }

        private void CopyFiles(bool showCompletedMessage)
        {
            Utils.Trace("CopyFiles enter");

            BackupDisk disk = SetupBackupDisk();

            Utils.LogWithPushover(BackupAction.BackupFiles, "Started");

            UpdateStatusLabel("Copying");

            IEnumerable<BackupFile> filesToBackup = mediaBackup.GetBackupFilesWithDiskEmpty().OrderByDescending(q => q.Length);

            long sizeOfFiles = filesToBackup.Sum(x => x.Length);

            int totalFileCount = filesToBackup.Count();

            Utils.LogWithPushover(BackupAction.BackupFiles,
                                  $"{totalFileCount:n0} files to backup at {Utils.FormatSize(sizeOfFiles)}"
                                 );

            bool outOfDiskSpaceMessageSent = false;
            bool result = false;

            int fileCounter = 0;
            long lastCopySpeed = 0;
            long remainingSizeOfFilesToCopy = sizeOfFiles;

            long copiedSoFar = 0;

            _ = Utils.GetDiskInfo(backupDiskTextBox.Text, out long availableSpace, out long totalBytes);

            long remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk);
            long sizeOfCopy = remainingDiskSpace < sizeOfFiles ? remainingDiskSpace : sizeOfFiles;

            /// We use 100 as the max because the actual number of bytes could be far too large 
            EnableProgressBar(0, 100);

            foreach (BackupFile backupFile in filesToBackup)
            {
                try
                {
                    fileCounter++;
                    UpdateStatusLabel("Copying", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

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

                    if (File.Exists(destinationFileName))
                    {
                        Utils.LogWithPushover(BackupAction.BackupFiles, $"[{fileCounter}/{totalFileCount}]\nSkipping copy of {sourceFileName} as it exists already.");
                        UpdateStatusLabel($"Skipping {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

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
                        UpdateStatusLabel($"Copying {Path.GetFileName(sourceFileName)}", Convert.ToInt32(copiedSoFar * 100 / sizeOfCopy));

                        result = Utils.GetDiskInfo(backupDiskTextBox.Text, out availableSpace, out totalBytes);

                        if (availableSpace > Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk) + sourceFileInfo.Length)
                        {
                            outOfDiskSpaceMessageSent = false;

                            string formattedEndDateTime = string.Empty;

                            if (lastCopySpeed > 0)
                            {
                                // copy speed is known
                                // remaining size is the smallest of remaining disk size-crital space to leave free OR
                                // size of remaining files to copy

                                remainingDiskSpace = availableSpace - Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk);

                                long sizeOfCopyRemaining = remainingDiskSpace < remainingSizeOfFilesToCopy ? remainingDiskSpace : remainingSizeOfFilesToCopy;
                                double numberOfSecondsOfCopyRemaining = sizeOfCopyRemaining / lastCopySpeed;

                                DateTime rightNow = DateTime.Now;

                                DateTime estimatedFinishDateTime = rightNow.AddSeconds(numberOfSecondsOfCopyRemaining);
                                formattedEndDateTime = ". Estimated finish by " + estimatedFinishDateTime.ToString("HH:mm");

                                // could be the following day
                                if (estimatedFinishDateTime.DayOfWeek != rightNow.DayOfWeek)
                                {
                                    formattedEndDateTime = ". Estimated finish by tomorrow at " + estimatedFinishDateTime.ToString("HH:mm");
                                }
                            }

                            Utils.LogWithPushover(BackupAction.BackupFiles,
                                                  $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nCopying {sourceFileName} at {sourceFileSize}{formattedEndDateTime}");

                            Utils.FileDelete(destinationFileNameTemp);

                            DateTime startTime = DateTime.UtcNow;
                            _ = Utils.FileCopyNewProcess(sourceFileName, destinationFileNameTemp);
                            DateTime endTime = DateTime.UtcNow;

                            // We need to check this here in case Cancel was clicked during the copy of the file
                            if (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); }

                            Utils.FileMove(destinationFileNameTemp, destinationFileName);

                            double timeTaken = (endTime - startTime).TotalSeconds;
                            Utils.Trace($"timeTaken {timeTaken}");
                            Utils.Trace($"sourceFileInfo.Length {sourceFileInfo.Length}");

                            lastCopySpeed = timeTaken > 0 ? Convert.ToInt64(sourceFileInfo.Length / timeTaken) : 0;

                            string copySpeed = lastCopySpeed > 0 ? Utils.FormatSpeed(lastCopySpeed) : "a very fast speed";

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
                                                      $"[{fileCounter}/{totalFileCount}] {Utils.FormatSize(availableSpace)} free.\nSkipping {sourceFileName} as not enough free space"
                                                      );
                                outOfDiskSpaceMessageSent = true;
                            }
                        }
                    }

                    remainingSizeOfFilesToCopy -= backupFile.Length;
                    copiedSoFar += backupFile.Length;
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
            UpdateStatusLabel("Saved.");

            IEnumerable<BackupFile> filesStillNotOnBackupDisk = mediaBackup.GetBackupFilesWithDiskEmpty();

            string text = string.Empty;

            if (filesStillNotOnBackupDisk.Count() > 0)
            {
                sizeOfFiles = filesStillNotOnBackupDisk.Sum(p => p.Length);

                text = $"{filesStillNotOnBackupDisk.Count():n0} files still to backup at {Utils.FormatSize(sizeOfFiles)}.\n";
            }

            Utils.LogWithPushover(BackupAction.BackupFiles, text + $"{disk.FreeFormatted} free on backup disk");

            if (showCompletedMessage)
            {
                Utils.LogWithPushover(BackupAction.BackupFiles, PushoverPriority.High, "Completed");
            }

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

            Utils.Log($"{filesNotOnBackupDisk.Count()} files at {Utils.FormatSize(filesNotOnBackupDisk.Sum(p => p.Length))}");

            Utils.Trace("ListFilesNotOnBackupDiskButton_Click exit");
        }

        private void RecalculateAllHashesButton_Click(object sender, EventArgs e)
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

        private void CopyFilesAsync(bool showCompletedMessage)
        {
            DisableControlsForAsyncTasks();

            CopyFiles(showCompletedMessage);

            ResetAllControls();
        }

        private void CheckConnectedDiskAndCopyFilesAsync(bool deleteExtraFiles, bool copyFiles)
        {
            DisableControlsForAsyncTasks();

            _ = CheckConnectedDisk(deleteExtraFiles);
            if (copyFiles) { CopyFiles(true); }

            ResetAllControls();
        }

        private void CheckConnectedDiskAndCopyFilesRepeaterAsync(bool copyFiles)
        {
            DisableControlsForAsyncTasks();
            string nextDiskMessage = "Please insert the next backup disk now";

            while (true)
            {
                BackupDisk lastBackupDiskChecked = CheckConnectedDisk(true);

                if (lastBackupDiskChecked == null)
                {
                    _ = MessageBox.Show("There was an error checking the connected backup disk", "Error on connected backup disk",
                                             MessageBoxButtons.OK);
                    continue;
                }

                if (copyFiles) { CopyFiles(false); }

                // send pushover high to change disk
                Utils.LogWithPushover(BackupAction.General,
                             PushoverPriority.High,
                             $"Backup disk {lastBackupDiskChecked.Name} checked. Please insert the next disk now");

                UpdateStatusLabel(nextDiskMessage);

                BackupDisk newDisk;
                do
                {
                    WaitForNewDisk(nextDiskMessage);
                    newDisk = SetupBackupDisk();
                } while (newDisk.Name == lastBackupDiskChecked.Name);
            }
        }

        private void ScanFolderAsync()
        {
            DisableControlsForAsyncTasks();

            ScanFolders();

            ResetAllControls();
        }

        public void TaskWrapper(Action methodName)
        {
            if (methodName is null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task t = Task.Run(() => methodName(), ct).ContinueWith(u =>
            {
                if (u.Exception != null)
                {
                    Utils.Log($"Exception occured. Cancelling operation.");
                    _ = MessageBox.Show($"Exception occured. Cancelling operation. {u.Exception}");

                    CancelButton_Click(null, null);
                }
            }, default
                , TaskContinuationOptions.OnlyOnFaulted
                , TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void TaskWrapper(Action<bool> methodName, bool param1)
        {
            if (methodName is null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task t = Task.Run(() => methodName(param1), ct).ContinueWith(u =>
            {
                if (u.Exception != null)
                {
                    Utils.Log($"Exception occured. Cancelling operation.");
                    _ = MessageBox.Show($"Exception occured. Cancelling operation. {u.Exception}");

                    CancelButton_Click(null, null);
                }
            }, default
                , TaskContinuationOptions.OnlyOnFaulted
                , TaskScheduler.FromCurrentSynchronizationContext());
        }

        public void TaskWrapper(Action<bool, bool> methodName, bool param1, bool param2)
        {

            if (methodName is null)
            {
                throw new ArgumentNullException(nameof(methodName));
            }

            tokenSource = new CancellationTokenSource();
            ct = tokenSource.Token;
            Task t = Task.Run(() => methodName(param1, param2), ct).ContinueWith(u =>
            {
                if (u.Exception != null)
                {
                    Utils.Log($"Exception occured. Cancelling operation.");
                    _ = MessageBox.Show($"Exception occured. Cancelling operation. {u.Exception}");

                    CancelButton_Click(null, null);
                }
            }, default
                , TaskContinuationOptions.OnlyOnFaulted
                , TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void DisableControlsForAsyncTasks()
        {
            if (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); }

            foreach (Control c in Controls)
            {
                if (!(c is StatusStrip))
                {
                    c.Invoke(x => x.Enabled = false);
                }
            }

            cancelButton.Invoke(x => x.Enabled = true);
            testPushoverEmergencyButton.Invoke(x => x.Enabled = true);
            testPushoverHighButton.Invoke(x => x.Enabled = true);
            testPushoverNormalButton.Invoke(x => x.Enabled = true);
            testPushoverLowButton.Invoke(x => x.Enabled = true);
            listFilesInMasterFolderButton.Invoke(x => x.Enabled = true);
            listFilesNotCheckedInXXButton.Invoke(x => x.Enabled = true);
            listFilesNotOnBackupDiskButton.Invoke(x => x.Enabled = true);
            listFilesOnBackupDiskButton.Invoke(x => x.Enabled = true);
            listFilesWithDuplicateContentHashcodesButton.Invoke(x => x.Enabled = true);
            listMoviesWithMultipleFilesButton.Invoke(x => x.Enabled = true);
            processesGroupBox.Invoke(x => x.Enabled = true);
            pushoverGroupBox.Invoke(x => x.Enabled = true);
            monitoringButton.Invoke(x => x.Enabled = true);
            reportBackupDiskStatusButton.Invoke(x => x.Enabled = true);
            listFilesGroupBox.Invoke(x => x.Enabled = true);
            listFilesOnBackupDiskGroupBox.Invoke(x => x.Enabled = true);

            if (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); }
        }

        /// <summary>
        /// Re-enables all the form controls (typically after Cancel was clicked or we've finished)
        /// </summary>
        private void ResetAllControls()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            foreach (Control c in Controls)
            {
                c.Invoke(x => x.Enabled = true);
            }

            cancelButton.Invoke(x => x.Enabled = false);
            statusStrip.Invoke(x => toolStripProgressBar.Visible = false);
            statusStrip.Invoke(x => toolStripStatusLabel.Text = string.Empty);
        }

        private void UpdateMasterFilesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("UpdateMasterFilesButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want to rebuild the master list?",
                                                  "Rebuild master list",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                TaskWrapper(ScanFolderAsync);
            }

            Utils.Trace("UpdateMasterFilesButton_Click exit");
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
                                              filter.Replace(".", @"\.")
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
        private void UpdateStatusLabel(string text, int value)
        {
            if (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); }

            string textToUse = string.Empty;

            if (value > 0)
            {
                int progress = value * 100 / toolStripProgressBar.Maximum;
                if (progress == 0) { progress = 1; }
                else
                if (progress == 100) { progress = 99; }

                textToUse = $"{text}     {progress}%";
            }
            else
            {
                if (!text.EndsWith("...") && !text.EndsWith("."))
                {
                    textToUse = text + "...";
                }
            }

            UpdateProgressBar(value);

            statusStrip.Invoke(x => toolStripStatusLabel.Text = textToUse);

            if (ct.IsCancellationRequested) { ct.ThrowIfCancellationRequested(); }
        }

        private void UpdateStatusLabel(string text)
        {
            UpdateStatusLabel(text, 0);
        }

        private void EnableProgressBar(int minimum, int maximum)
        {
            if (maximum >= minimum)
            {
                statusStrip.Invoke(x => toolStripProgressBar.Minimum = minimum);
                statusStrip.Invoke(x => toolStripProgressBar.Maximum = maximum);
                statusStrip.Invoke(x => toolStripProgressBar.Visible = true);
                statusStrip.Invoke(x => toolStripProgressBar.Value = minimum);
            }
        }

        private void UpdateProgressBar(int value)
        {
            if (value > 0)
            {
                statusStrip.Invoke(x => toolStripProgressBar.Visible = true);
                statusStrip.Invoke(x => toolStripProgressBar.Value = value);
            }
            else
            {
                statusStrip.Invoke(x => toolStripProgressBar.Value = toolStripProgressBar.Minimum);
                statusStrip.Invoke(x => toolStripProgressBar.Visible = false);
            }
        }

        private void ScanFolders()
        {
            Utils.Trace("ScanFolders enter");

            string filters = string.Join(",", mediaBackup.Filters.ToArray());

            long readSpeed = 0, writeSpeed = 0;

            mediaBackup.ClearFlags();

            Utils.LogWithPushover(BackupAction.ScanFolders, "Started");
            UpdateStatusLabel("Started");
            UpdateProgressBar(1);

            foreach (string masterFolder in mediaBackup.MasterFolders)
            {
                UpdateStatusLabel($"Scanning {masterFolder}");

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

                    _ = Utils.GetDiskInfo(masterFolder, out long freeSpaceOnCurrentMasterFolder, out long totalBytesOnMasterFolderDisk);

                    if (mediaBackup.DiskSpeedTests)
                    {
                        UpdateStatusLabel($"Speed testing {masterFolder}");
                        Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.SpeedTestFileSize), mediaBackup.SpeedTestIterations, out readSpeed, out writeSpeed);
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

                    UpdateStatusLabel($"Scanning {masterFolder}");

                    // Check for files in the root of the master folder alongside te indexfolders
                    string[] filesInRootOfMasterFolder = Utils.GetFiles(masterFolder, filters, SearchOption.TopDirectoryOnly);

                    foreach (string file in filesInRootOfMasterFolder)
                    {
                        Utils.Trace($"Checking {file}");

                        // Check for files to delete
                        if (CheckForFilesToDelete(file))
                        {
                            continue;
                        }
                    }

                    foreach (string indexFolder in mediaBackup.IndexFolders)
                    {
                        string folderToCheck = Path.Combine(masterFolder, indexFolder);

                        if (Directory.Exists(folderToCheck))
                        {
                            Utils.LogWithPushover(BackupAction.ScanFolders, $"{folderToCheck}");
                            UpdateStatusLabel($"Scanning {folderToCheck}");

                            string[] files = Utils.GetFiles(folderToCheck, filters, SearchOption.AllDirectories);

                            EnableProgressBar(0, files.Length);

                            for (int i = 0; i < files.Length; i++)
                            {
                                string file = files[i];
                                Utils.Trace($"Checking {file}");

                                UpdateStatusLabel($"Scanning {folderToCheck}", i + 1);

                                // Check for files to delete
                                if (CheckForFilesToDelete(file))
                                {
                                    continue;
                                }

                                // RegEx file name rules
                                foreach (FileRule rule in mediaBackup.FileRules)
                                {
                                    if (Regex.IsMatch(file, rule.FileDiscoveryRegEx))
                                    {
                                        if (!rule.Matched)
                                        {
                                            Utils.Trace($"{rule.Name} matched file {file}");
                                            rule.Matched = true;
                                        }

                                        // if it does then the second regex must be true
                                        if (!Regex.IsMatch(file, rule.FileTestRegEx))
                                        {
                                            Utils.Trace($"File {file} matched by {rule.FileDiscoveryRegEx} but doesn't match {rule.FileTestRegEx}");
                                            Utils.LogWithPushover(BackupAction.ScanFolders, PushoverPriority.High, $"{rule.Name} {rule.Message} {file}");
                                        }
                                    }
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

            UpdateStatusLabel($"Scanning completed.");

            foreach (FileRule rule in mediaBackup.FileRules)
            {
                if (!rule.Matched)
                {
                    Utils.LogWithPushover(BackupAction.ScanFolders,
                                         PushoverPriority.High,
                                         $"{rule.Name} didn't match any files");
                }
            }

            mediaBackup.RemoveFilesWithFlag(false, true);
            mediaBackup.Save();

            UpdateStatusLabel($"Saved.");

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

                int days = DateTime.Today.Subtract(oldestFileDate).Days;

                string daysText = days == 1 ? string.Empty : "(s)";

                Utils.LogWithPushover(BackupAction.ScanFolders,
                                      $"Oldest backup date is {days:n0} day{daysText} ago on {oldestFileDate.ToShortDateString()} on {oldestFile.Disk}");
            }

            Utils.LogWithPushover(BackupAction.ScanFolders,
                                  $"{filesNotOnBackupDisk.Count():n0} files to backup at {Utils.FormatSize(fileSizeToCopy)}");

            Utils.LogWithPushover(BackupAction.ScanFolders, "Completed");

            Utils.Trace("ScanFolders exit");
        }

        #endregion

        private void CheckDiskAndDeleteButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("checkDiskAndDeleteButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
                                                  "Delete extra files",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, false);
            }

            Utils.Trace("checkDiskAndDeleteButton_Click exit");
        }

        private void BackupTimerButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("timerButton_Click enter");

            if (mediaBackup.StartScheduledBackup)
            {
                mediaBackup.StartScheduledBackup = false;
                trigger.OnTimeTriggered -= scheduledBackupAction;
            }
            else
            {
                mediaBackup.StartScheduledBackup = true;
                // Fire once if CheckBox is ticked
                if (runOnTimerStartCheckBox.Checked)
                {
                    TaskWrapper(ScheduledBackupAsync);
                }

                trigger = new DailyTrigger(Convert.ToInt32(hoursNumericUpDown.Value), Convert.ToInt32(minutesNumericUpDown.Value));
                trigger.OnTimeTriggered += scheduledBackupAction;
            }

            UpdateScheduledBackupButton();
            Utils.Trace("timerButton_Click exit");
        }

        private void ScheduledBackupAsync()
        {
            DisableControlsForAsyncTasks();

            ScheduledBackup();

            ResetAllControls();
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

                if (mediaBackup.StartMonitoring)
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
                _ = CheckConnectedDisk(true);

                // Copy any files that need a backup
                CopyFiles(true);
            }
            catch (OperationCanceledException)
            {
            }

            catch (Exception ex)
            {
                Utils.LogWithPushover(BackupAction.General,
                                      PushoverPriority.Emergency,
                                      $"Exception occured {ex}");
            }

            Utils.Trace("ScheduledBackup exit");
        }

        private void ListFilesOnBackupDiskButton_Click(object sender, EventArgs e)
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

        private void ListFilesInMasterFolderButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("listFilesInMasterFolderButton_Click enter");

            string masterFolder = listMasterFoldersComboBox.SelectedItem.ToString();

            IEnumerable<BackupFile> files = mediaBackup.GetBackupFilesInMasterFolder(masterFolder);

            Utils.Log($"Listing files in master folder {masterFolder}");

            long readSpeed = 0, writeSpeed = 0;
            if (mediaBackup.DiskSpeedTests)
            {
                Utils.DiskSpeedTest(masterFolder, Utils.ConvertMBtoBytes(mediaBackup.SpeedTestFileSize), mediaBackup.SpeedTestIterations, out readSpeed, out writeSpeed);
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

        private void RestoreFilesButton_Click(object sender, EventArgs e)
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
                    _ = MessageBox.Show("You must select a master folder that you'd files the files from backup disks restored for. This is typically the drive that is now failing",
                                    "Restore backup files",
                                    MessageBoxButtons.OK);
                    return;
                }

                string masterFolder = masterFoldersComboBox.SelectedItem.ToString();

                if (restoreMasterFolderComboBox.SelectedItem == null)
                {
                    _ = MessageBox.Show("You must select a master folder that you'd files the files from backup copied to. This is typically a new drive that will replace the failing drive",
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
                            _ = MessageBox.Show("Cannot connect to the backup drive required", "Restore backup files", MessageBoxButtons.OK);
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
                            Utils.LogWithPushover(BackupAction.Restore,
                                                  $"[{fileCounter}/{countOfFiles}] {targetFilePath} Already exists");
                        }
                        else
                        {
                            if (File.Exists(sourceFileFullPath))
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

                        if (File.Exists(targetFilePath))
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

        private void CheckBackupDeleteAndCopyButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("checkBackupDeleteAndCopyButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
                                                  "Delete extra files",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                TaskWrapper(CheckConnectedDiskAndCopyFilesAsync, true, true);
            }

            Utils.Trace("checkBackupDeleteAndCopyButton_Click exit");
        }

        private void ListMoviesWithMultipleFilesButton_Click(object sender, EventArgs e)
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

        private void TestPushoverHighButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverHighButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.High,
                                  "High priority test");

            Utils.Trace("testPushoverHighButton_Click exit");
        }

        private void TestPushoverNormalButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverNormalButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.Normal,
                                  "Normal priority test\nLine 2\nLine 3");

            Utils.Trace("testPushoverNormalButton_Click exit");
        }

        private void TestPushoverEmergencyButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("testPushoverEmergencyButton_Click enter");

            Utils.LogWithPushover(BackupAction.General,
                                  PushoverPriority.Emergency,
                                  PushoverRetry.OneMinute,
                                  PushoverExpires.OneHour,
                                  "Emergency priority test");

            Utils.Trace("testPushoverEmergencyButton_Click exit");
        }

        private void ReportBackupDiskStatusButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("reportBackupDiskStatusButton_Click enter");

            IEnumerable<BackupDisk> disks = mediaBackup.BackupDisks.OrderBy(p => p.Number);

            Utils.Log("Listing backup disk statuses");

            long actualUsuableSpace = 0;
            foreach (BackupDisk disk in disks)
            {
                DateTime d = DateTime.Parse(disk.Checked);

                long totalSizeOfFilesFromSumOfFiles = mediaBackup.GetBackupFilesOnBackupDisk(disk.Name).Sum(p => p.Length);

                long sizeFromDiskAnalysis = disk.Capacity - disk.Free;

                long difference = totalSizeOfFilesFromSumOfFiles > sizeFromDiskAnalysis ? 0 : sizeFromDiskAnalysis - totalSizeOfFilesFromSumOfFiles;
                double percentageDiff = difference * 100 / sizeFromDiskAnalysis;

                Utils.Log($"{disk.Name,-10}   Last check: {d:dd-MMM-yy}   Capacity: {disk.CapacityFormatted,-7}   Used: {Utils.FormatSize(sizeFromDiskAnalysis),-8}   Free: {disk.FreeFormatted,-7}   Sum of files: {Utils.FormatSize(totalSizeOfFilesFromSumOfFiles),-7}   Diff: {Utils.FormatSize(difference),-6} {percentageDiff}%");

                if (disk.Free > Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk))
                {
                    actualUsuableSpace += disk.Free - Utils.ConvertMBtoBytes(mediaBackup.MinimumFreeSpaceToLeaveOnBackupDisk);
                }
            }

            string totalSizeFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Capacity));
            string totalFreespaceFormatted = Utils.FormatSize(mediaBackup.BackupDisks.Sum(p => p.Free));

            Utils.Log($"Total Capacity: {totalSizeFormatted} Free: {totalFreespaceFormatted}  UsuableSpace: {Utils.FormatSize(actualUsuableSpace)} Sum of files: {Utils.FormatSize(mediaBackup.BackupFiles.Sum(p => p.Length))}");

            Utils.Trace("reportBackupDiskStatusButton_Click exit");
        }

        private void SpeedTestButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("speedTestButton_Click enter");

            TaskWrapper(SpeedTestAllMasterFoldersAsync);

            Utils.Trace("speedTestButton_Click exit");
        }

        private void SpeedTestAllMasterFoldersAsync()
        {
            DisableControlsForAsyncTasks();

            Utils.Log("Speed testing all master folders");

            EnableProgressBar(0, mediaBackup.MasterFolders.Count);

            for (int i = 0; i < mediaBackup.MasterFolders.Count; i++)
            {
                string masterFolder = mediaBackup.MasterFolders[i];

                UpdateStatusLabel($"Speed testing {masterFolder}", i + 1);

                if (Utils.IsFolderWritable(masterFolder))
                {
                    Utils.DiskSpeedTest(masterFolder,
                                        Utils.ConvertMBtoBytes(mediaBackup.SpeedTestFileSize),
                                        mediaBackup.SpeedTestIterations,
                                        out long readSpeed,
                                        out long writeSpeed);
                    Utils.Log($"testing {masterFolder}, Read: {Utils.FormatSpeed(readSpeed)} Write: {Utils.FormatSpeed(writeSpeed)}");
                }
            }

            ResetAllControls();
        }

        private void MinutesNumericUpDown_ValueChanged(object sender, EventArgs e)
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

        private void HoursNumericUpDown_ValueChanged(object sender, EventArgs e)
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

        private void MonitoringButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("monitoringButton_Click enter");

            if (mediaBackup.StartMonitoring)
            {
                monitoringTimer.Stop();
                Utils.LogWithPushover(BackupAction.Monitoring, "Stopped");

                mediaBackup.StartMonitoring = false;
                UpdateMonitoringButton();
            }
            else
            {
                Utils.LogWithPushover(BackupAction.Monitoring, "Started");

                MonitoringTimer_Tick(null, null);

                monitoringTimer.Interval = mediaBackup.MonitorInterval * 1000;
                monitoringTimer.Start();

                mediaBackup.StartMonitoring = true;
                UpdateMonitoringButton();
            }

            Utils.Trace("monitoringButton_Click exit");
        }

        private void MonitoringTimer_Tick(object sender, EventArgs e)
        {
            _ = monitoringAction.BeginInvoke(monitoringAction.EndInvoke, null);
        }

        private void MonitorServices()
        {
            //Utils.Trace("MonitorServices enter");

            foreach (ProcessServiceMonitor monitor in mediaBackup.Monitors)
            {
                bool result = monitor.Port > 0 ? Utils.ConnectionExists(monitor.Url, monitor.Port) : Utils.UrlExists(monitor.Url, monitor.Timeout * 1000);

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

                        _ = Utils.KillProcesses(monitor.ProcessToKill);
                    }

                    if (monitor.ApplicationToStart.HasValue())
                    {
                        text = $"Starting {monitor.ApplicationToStart}";

                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.Normal,
                                              text);

                        string processToStart = Environment.ExpandEnvironmentVariables(monitor.ApplicationToStart);

                        if (File.Exists(processToStart))
                        {
                            Process newProcess = Process.Start(processToStart, monitor.ApplicationToStartArguments);

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

            //Utils.Trace("MonitorServices exit");
        }

        private void KillProcessesButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("killProcessesButton_Click enter");

            foreach (ProcessServiceMonitor monitor in mediaBackup.Monitors)
            {
                if (monitor.ProcessToKill.HasValue())
                {
                    Utils.LogWithPushover(BackupAction.Monitoring,
                                          PushoverPriority.Normal,
                                          $"Stopping all '{monitor.ProcessToKill}' processes that match");

                    _ = Utils.KillProcesses(monitor.ProcessToKill);
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

        private void TestPushoverLowButton_Click(object sender, EventArgs e)
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

            Utils.BackupLogFile();
        }

        private void StopProcessButton_Click(object sender, EventArgs e)
        {
            Utils.Trace("stopProcessButton_Click enter");

            if (processesComboBox.SelectedItem != null)
            {
                string monitorName = processesComboBox.SelectedItem.ToString();

                ProcessServiceMonitor monitor = mediaBackup.Monitors.First(m => m.Name == monitorName);

                if (monitor.ProcessToKill.HasValue())
                {
                    string[] processesToKill = monitor.ProcessToKill.Split(',');

                    foreach (string toKill in processesToKill)
                    {
                        Utils.LogWithPushover(BackupAction.Monitoring,
                                              PushoverPriority.Normal,
                                              $"Stopping all '{toKill}' processes that match");

                        _ = Utils.KillProcesses(toKill);
                    }
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

        private void ListFilesWithDuplicateContentHashcodesButton_Click(object sender, EventArgs e)
        {
            Utils.Log("Checking for files with Duplicate ContentsHash");

            mediaBackup.ClearFlags();

            foreach (BackupFile f in mediaBackup.BackupFiles)
            {
                string fullPath = f.FullPath;

                if (fullPath.StartsWith("\\\\nas2\\assets3\\_Backup") ||
                    fullPath.StartsWith("\\\\nas2\\assets3\\_Other") ||
                    fullPath.StartsWith("\\\\nas2\\assets3\\_From Mum") ||
                    fullPath.StartsWith("\\\\nas2\\assets3\\_Software") ||
                    fullPath.EndsWith(".srt"))
                {
                    f.Flag = true;
                }
            }

            BackupFile backupFileToDelete;
            long length = 0;

            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < mediaBackup.BackupFiles.Count; i++)
            {
                BackupFile a = mediaBackup.BackupFiles[i];

                for (int j = i + 1; j < mediaBackup.BackupFiles.Count; j++)
                {
                    BackupFile b = mediaBackup.BackupFiles[j];
                    backupFileToDelete = null;

                    if (!a.Flag && !b.Flag && a.Length != 0 && (b.ContentsHash == a.ContentsHash) && (b.FullPath != a.FullPath))
                    {
                        Utils.Log($"[{i + 1}/{mediaBackup.BackupFiles.Count}] {a.FullPath} has copy at {b.FullPath}");
                        b.Flag = true;

                        if (a.FullPath.Contains("\\_Pictures"))
                        {
                            if (a.FileName.Contains("IMG_") && !b.FileName.Contains("IMG_"))
                            {
                                backupFileToDelete = a;
                            }
                            else if (!a.FileName.Contains("IMG_") && b.FileName.Contains("IMG_"))
                            {

                                backupFileToDelete = b;
                            }

                            else if (a.FileName.Contains("SAM_") && !b.FileName.Contains("SAM_"))
                            {
                                backupFileToDelete = a;
                            }
                            else if (!a.FileName.Contains("SAM_") && b.FileName.Contains("SAM_"))
                            {

                                backupFileToDelete = b;
                            }
                            else if (a.FileName.Contains("(") && !b.FileName.Contains("("))
                            {
                                backupFileToDelete = a;
                            }
                            else if (!a.FileName.Contains("(") && b.FileName.Contains("("))
                            {
                                backupFileToDelete = b;
                            }
                            else if (a.FileName.Contains("Copy") && !b.FileName.Contains("Copy"))
                            {
                                backupFileToDelete = a;
                            }
                            else if (!a.FileName.Contains("Copy") && b.FileName.Contains("Copy"))
                            {
                                backupFileToDelete = b;
                            }

                            else if (a.FileName.Length <= b.FileName.Length)
                            {
                                backupFileToDelete = b;
                            }
                            if (backupFileToDelete != null)
                            {
                                Utils.Log($"delete {backupFileToDelete.FullPath}");
                                _ = sb.AppendLine($"DEL /P \"{backupFileToDelete.FullPath}\"");
                                length += backupFileToDelete.Length;
                            }
                        }
                    }
                }
            }

            Utils.Log($"Total size of files to delete is {Utils.FormatSize(length)}");
            Utils.Log(sb.ToString());

            Utils.Log("Finshed checking for files with Duplicate ContentsHash");
        }

        private void CheckDeleteAndCopyAllBackupDisksButton_Click(object sender, EventArgs e)
        {
            // Check the current backup disk connected
            // when its finished prompt for another disk and wait

            Utils.Trace("CheckDeleteAndCopyAllBackupDisksButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
                                                  "Delete extra files",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, true);
            }

            Utils.Trace("CheckDeleteAndCopyAllBackupDisksButton_Click exit");
        }

        private void WaitForNewDisk(string message)
        {
            Utils.Trace("WaitForNewDisk enter");

            UpdateStatusLabel(message);
            Task.Delay(5000).Wait();

            Utils.Trace("WaitForNewDisk exit");
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            tokenSource.Cancel();

            toolStripStatusLabel.Text = "Cancelling ...";

            if (Utils.CopyProcess != null && !Utils.CopyProcess.HasExited)
            {
                Utils.CopyProcess?.Kill();
            }

            cancelButton.Enabled = false;
            ResetAllControls();
        }

        private void CheckAllBackupDisksButton_Click(object sender, EventArgs e)
        {
            // Check the current backup disk connected
            // when its finished prompt for another disk and wait

            Utils.Trace("CheckAllBackupDisksButton_Click enter");

            DialogResult answer = MessageBox.Show("Are you sure you want delete any extra files on the backup disk not in our list?",
                                                  "Delete extra files",
                                                  MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                TaskWrapper(CheckConnectedDiskAndCopyFilesRepeaterAsync, false);
            }

            Utils.Trace("CheckAllBackupDisksButton_Click exit");
        }

        private void PushoverOnOffButton_Click(object sender, EventArgs e)
        {
            mediaBackup.StartSendingPushoverMessages = !mediaBackup.StartSendingPushoverMessages;
            UpdateSendingPushoverButton();

        }

        private void UpdateSendingPushoverButton()
        {
            pushoverOnOffButton.Text = mediaBackup.StartSendingPushoverMessages ? "Sending = ON" : "Sending = OFF";
        }
        private void UpdateMonitoringButton()
        {
            monitoringButton.Text = mediaBackup.StartMonitoring == true ? "Monitoring = ON" : "Monitoring = OFF";
        }

        private void UpdateScheduledBackupButton()
        {
            scheduledBackupTimerButton.Text = mediaBackup.StartScheduledBackup == true ? "Backup = ON" : "Backup = OFF";
        }
    }
}