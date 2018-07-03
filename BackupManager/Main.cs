
namespace BackupManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
    using System.Management;
    using System.Configuration;

    using BackupManager.Entities;

    // We scan MasterFolder shares looking for all the IndexFolders contents

    public partial class Main : Form
    {
        #region Constants

        /// <summary>
        /// The backup drive prefix.
        /// </summary>
        private const string BackupDrivePrefix = "Backup ";

        private const long MinimumFreeSpaceToLeaveOnBackupDrive = 10 * 1024 * 1024; // 10MB in bytes

        #endregion

        #region Fields

        private readonly MediaBackup mediaBackup;

        #endregion

        #region Constructors and Destructors

        public Main()
        {
            this.InitializeComponent();

            string MediabackupXml = ConfigurationManager.AppSettings.Get("MediabackupXml");

            string localMediaXml = Path.Combine(Application.StartupPath, "MediaBackup.xml");

            this.mediaBackup = MediaBackup.Load(File.Exists(localMediaXml) ? localMediaXml : MediabackupXml);

#if !DEBUG
            this.timerTextBox.Text = "1440";
#endif
            
            this.backupDiskTextBox.Text = Path.Combine(@"\\", System.Environment.MachineName, "backup");

            foreach (string a in this.mediaBackup.MasterFolders)
            {
                this.masterFoldersComboBox.Items.Add(a);
            }

            if (mediaBackup.ScheduledBackupRepeatInterval != 0)
            {
                timerTextBox.Text = mediaBackup.ScheduledBackupRepeatInterval.ToString();
            }

            if (mediaBackup.StartScheduledBackup)
            {
                timerButton_Click(null, null);
            }
        }

        #endregion

        #region Methods

        private static void CheckHashOnBackupDisk(
            string logFile,
            string fullPath,
            BackupFile backupFile,
            string driveName)
        {
            string hashFromFile = Utils.GetShortMd5HashFromFile(fullPath);

            if (hashFromFile == Utils.ZeroByteHash)
            {
                Utils.Log(logFile, BackupAction.CheckBackupDisk, "ERROR: {0} has zerobyte hashcode", fullPath);
            }


            backupFile.BackupDisk = driveName;

            if (hashFromFile == backupFile.ContentsHash)
            {
                // Hash matches so update backup checked date
                backupFile.BackupDiskChecked = DateTime.Now.ToString("yyyy-MM-dd");
            }
            else
            {
                Utils.Log(logFile, BackupAction.CheckBackupDisk, "ERROR: {0} has incorrect hashcode", fullPath);
                backupFile.BackupDiskChecked = null;

                // clear this too - means the backed up file will be removed on the next run
                backupFile.BackupDisk = null;
            }
        }

        private void BackupHashCodeCheckedButtonClick(object sender, EventArgs e)
        {
            string LogFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "backup_FilesWithoutBackupDiskChecked.txt");

            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "'{0}' does not have BackupDiskChecked set on backup disk {1}",
                    file.FullPath,
                    file.BackupDisk);
            }
        }

        private void CheckConnectedBackupDriveButtonClick(object sender, EventArgs e)
        {
                this.CheckConnectedDisk(false);
        }

        private void CheckConnectedDisk(bool deleteExtraFiles)
        {
            // Scans the connected backup disk and finds all its files
            // for each for found calculate the hash from the backup disk
            // find that hash in the backup data file
            // rebuilds the source filename from MasterFolder+IndexFolder+Path
            // checks the file still exists there
            // if it does compare the hashcodes and update results
            // force a recalc of both the hashes to check the files can both be read correctly

            string logFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_CheckBackupDisk.txt");

            Utils.Log(logFile, BackupAction.CheckBackupDisk, "Started.");

            string backupShare = this.backupDiskTextBox.Text;

            // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
            // if not thats a problem
            if (!this.CheckForValidBackupShare(backupShare))
            {
                Utils.Log(logFile, BackupAction.CheckBackupDisk, "No connected backup disk detected.");
                return;
            }

            string backupFolderName = this.GetBackupFolderName(backupShare);

            string folderToCheck = Path.Combine(backupShare, backupFolderName);
 
            // reset the filters because we want to search for all extra files
            string filters = "*";

            IEnumerable<BackupFile> filesToReset =
                this.mediaBackup.BackupFiles.Where(
                    p =>
                    p.BackupDisk != null && p.BackupDisk.Equals(backupFolderName, StringComparison.CurrentCultureIgnoreCase));


            Utils.Log(logFile, BackupAction.CheckBackupDisk, "Checking {0} - {1}GB free", backupFolderName, Utils.GetDiskFreeSpace(folderToCheck));
 
            foreach (BackupFile file in filesToReset)
            {
                file.BackupDisk = null;
            }

            
            string[] backupDiskFiles = Utils.GetFiles(
                folderToCheck,
                filters,
                SearchOption.AllDirectories,
                FileAttributes.Hidden);

            foreach (string backupFileFullPath in backupDiskFiles)
            {
                string backupFileHash = Utils.GetShortMd5HashFromFile(backupFileFullPath);

                if (this.mediaBackup.Contains(backupFileHash, backupFileFullPath))
                {
                    BackupFile backupFile = this.mediaBackup.GetBackupFile(backupFileHash, backupFileFullPath);
                    Utils.Log(logFile, "Checking hash for {0}", backupFileFullPath);

                    // Reset the source file hash because we want to confirm the source file can be read 
                    backupFile.ContentsHash = Utils.GetShortMd5HashFromFile(backupFile.FullPath);
                    CheckHashOnBackupDisk(logFile, backupFileFullPath, backupFile, backupFolderName);

                    // So we can get the situation where the hash of a file on disk is equal to a hash of a file we have
                    // It could have a different filename on the backup disk though (if we renamed the master file)
                    // check the filenames are equal and rename on the backup disk if they're not

                    string backupDiskFilename = backupFileFullPath.Substring(folderToCheck.Length + 1); // trim off the unc and backup disk parts

                    string masterFilename = Path.Combine(backupFile.IndexFolder, backupFile.RelativePath);

                    if (!backupDiskFilename.Equals(masterFilename))
                    {
                        string destinationFileName = Path.Combine(folderToCheck, backupFile.IndexFolder, backupFile.RelativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                        Utils.Log(logFile, BackupAction.CheckBackupDisk, "Renaming {0} to {1}", backupFileFullPath, destinationFileName);

                        File.Move(backupFileFullPath, destinationFileName);
                    }
                }
                else
                {
                    // Extra file on a backup disk
                    if (deleteExtraFiles)
                    {
                        Utils.Log(
                            logFile, BackupAction.CheckBackupDisk,
                            "Extra file {0} on backup disk {1} now deleted.",
                            backupFileFullPath,
                            backupFolderName);

                        Utils.ClearFileAttribute(backupFileFullPath, FileAttributes.ReadOnly);

                        File.Delete(backupFileFullPath);

                    }
                    else
                    {
                        Utils.Log(
                            logFile, BackupAction.CheckBackupDisk,
                            "Extra file {0} on backup disk {1}",
                            backupFileFullPath,
                            backupFolderName);
                    }
                }
            }

            this.mediaBackup.Save();

            // Remove all empty folders
            DeleteEmptyDirectories(logFile, folderToCheck);

            Utils.Log(logFile, BackupAction.CheckBackupDisk, "Completed.");
        }

        private void DeleteEmptyDirectories(string logFile, string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                DeleteEmptyDirectories(logFile, directory);
                if (Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Utils.Log(logFile, BackupAction.CheckBackupDisk, "Deleting empty folder {0}", directory);
                    Directory.Delete(directory, false);
                }
            }
        }

        private string GetBackupFolderName(string sharePath)
        {
            var a = new DirectoryInfo(sharePath);

            var b = from file in a.GetDirectories()
                    where
                        (((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0))
                    select file;

            // In here there should be 1 directory starting with 'backup '

            if (b.Count() != 1)
            {
                 MessageBox.Show("Too many folders in the backup share. There should be only 1.", "Backup Manager", MessageBoxButtons.OK);
                return null;
            }
            var c = b.Single();

            if (!c.Name.StartsWith("backup ",StringComparison.CurrentCultureIgnoreCase))
            {
                 MessageBox.Show("Folder in the backup share shoud start with 'backup ' and then a space and a 2 digit number.", "Backup Manager", MessageBoxButtons.OK);
                return null;
            }

            return c.Name;
        }

        private bool CheckForValidBackupShare(string sharePath)
        {
            var a = new DirectoryInfo(sharePath);

            var b = from file in a.GetDirectories()
                    where
                        (((file.Attributes & FileAttributes.Hidden) == 0) & ((file.Attributes & FileAttributes.System) == 0))
                    select file;

            if (b.Count() == 0)
            {
                MessageBox.Show("No folders in the backup share.", "Backup Manager", MessageBoxButtons.OK);
                return false;
            }
            if (b.Count() != 1)
            {
                 MessageBox.Show("Too many folders in the backup share. There should be only 1.", "Backup Manager", MessageBoxButtons.OK);
                return false;
            }

            var c = b.Single();

            if (!c.Name.StartsWith("backup ",StringComparison.CurrentCultureIgnoreCase))
            {
                 MessageBox.Show("Folder in the backup share shoud start with 'backup ' and then a space and a 2 digit number.", "Backup Manager", MessageBoxButtons.OK);
                return false;
            }

            return true;
        }

        private void CopyFilesToBackupDriveButtonClick(object sender, EventArgs e)
        {
            CopyFiles();
        }

        private void CopyFiles()
        {
            string backupShare = this.backupDiskTextBox.Text;

            // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
            // if not thats a problem
            if (!this.CheckForValidBackupShare(backupShare))
            {
                return;
            }

            string backupFolderName = this.GetBackupFolderName(backupShare);

            string insertedBackupDrive = Path.Combine(backupShare, backupFolderName);

            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "backup_CopyMissingFilesToBackupDisk.txt");

            Utils.Log(logFile, BackupAction.BackupFiles, "Started.");

            IEnumerable<BackupFile> filesToBackup =
                this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            bool outOfDiskSpaceMessageSent = false;

            foreach (BackupFile backupFile in filesToBackup)
            {
                try
                {
                    if (string.IsNullOrEmpty(backupFile.IndexFolder))
                    {
                        throw new ApplicationException("Index folder is empty.");
                    }

                    string destinationFileName = Path.Combine(insertedBackupDrive, backupFile.IndexFolder, backupFile.RelativePath);
                    string sourceFileName = backupFile.FullPath;
                    FileInfo sourceFileInfo = new FileInfo(sourceFileName);

                    if (File.Exists(destinationFileName))
                    {
                        Utils.Log(logFile, "Skipping copy as it exists.");

                        // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                        // in which case check the source hash again and then check the copied file 
                        backupFile.ContentsHash = Utils.GetShortMd5HashFromFile(sourceFileName);
                        CheckHashOnBackupDisk(logFile, destinationFileName, backupFile, backupFolderName);
                    }
                    else
                    {
                        long availableSpace;

                        var b = Utils.GetDiskFreeSpace(backupShare, out availableSpace);

                        if (availableSpace > MinimumFreeSpaceToLeaveOnBackupDrive)
                        {
                            if (availableSpace > sourceFileInfo.Length)
                            {
                                outOfDiskSpaceMessageSent = false;

                                Utils.Log(logFile, BackupAction.BackupFiles, "Copying {0}", sourceFileName);
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                                File.Copy(sourceFileName, destinationFileName);

                                // Make sure its not readonly
                                Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                                Utils.Log(logFile, "Copy complete.");

                                // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                                // in which case check the source hash again and then check the copied file 
                                backupFile.ContentsHash = Utils.GetShortMd5HashFromFile(sourceFileName);




                                CheckHashOnBackupDisk(logFile, destinationFileName, backupFile, backupFolderName);
                            }
                            else
                            {
                                if (!outOfDiskSpaceMessageSent)
                                {
                                    Utils.Log(logFile, BackupAction.BackupFiles, "Skipping {0} as not enough free space", sourceFileName);
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

                catch (IOException)
                {
                    // Sometimes during a copy we get this if we lose the connection to the source NAS drive
                    Utils.Log(logFile, BackupAction.BackupFiles, "IOException during copy. Skipping file.");
                }
            }

            this.mediaBackup.Save();

            IEnumerable<BackupFile> filesNotOnBackupDisk = this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            if (filesNotOnBackupDisk.Count() > 0)
            {
                Utils.Log(logFile, BackupAction.ScanFolders, "{0:n0} files still to backup", filesNotOnBackupDisk.Count());
            }

            IEnumerable<BackupFile> filesWithoutDiskChecked = this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            if (filesWithoutDiskChecked.Count() > 0)
            {
                Utils.Log(logFile, BackupAction.ScanFolders, "{0:n0} files still without DiskChecked set", filesWithoutDiskChecked.Count());
            }

            Utils.Log(logFile, BackupAction.BackupFiles, "Completed.");
        }

        private void EnsureFile(string path, string masterFolder, string indexFolder)
        {
            // Empty Index folder is now no longer allowed
            if (string.IsNullOrEmpty(indexFolder))
            {
                throw new ApplicationException("IndexFolder is empty. Not supported.");
            }

            // if indexFolder is empty and we start with underscore then exclude it
            string pathAfterMaster = path.SubstringAfter(masterFolder, StringComparison.CurrentCultureIgnoreCase);

            if (pathAfterMaster.StartsWith(@"\_") && string.IsNullOrEmpty(indexFolder))
            {
                return;
            }

            BackupFile backupFile = this.mediaBackup.GetBackupFile(path, masterFolder, indexFolder);

            if (backupFile == null)
            {
                MessageBox.Show(string.Format("Duplicate hashcode detected indicated a copy of a file at {0}.", path), "DuplicateFile", MessageBoxButtons.OK);
            }

            if (string.IsNullOrEmpty(backupFile.ContentsHash))
            {
                throw new ApplicationException("Hash is null or empty.");
            }

            backupFile.Flag = true;
        }

        private void ListFilesNotOnBackupDriveButtonClick(object sender, EventArgs e)
        {
            string logFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "backup_FilesNotOnABackupDisk.txt");

            IEnumerable<BackupFile> filesNotOnBackupDisk =
                this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            foreach (BackupFile file in filesNotOnBackupDisk)
            {
                Utils.Log(logFile, "'{0}' does not have BackupDisk set.", file.FullPath);
            }
        }

        private void RecalculateHashcodesButtonClick(object sender, EventArgs e)
        {
            DialogResult answer =
                MessageBox.Show(
                    "Are you sure you want to recalculate the hashcodes from the master files?",
                    "Recalculate Hashcodes",
                    MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                foreach (BackupFile backupFile in this.mediaBackup.BackupFiles)
                {
                    backupFile.ContentsHash = Utils.GetShortMd5HashFromFile(backupFile.FullPath);
                }

                this.mediaBackup.Save();
            }
        }

        private void UpdateMasterFilesButtonClick(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
                "Are you sure you want to rebuild the master list?",
                "Rebuild master list",
                MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                this.ScanFolders();
            }
        }

        private void ScanFolders()
        {
            string logFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_BuildMasterFileList.txt");



            string filters = string.Join(",", this.mediaBackup.Filters.ToArray());

            this.mediaBackup.ClearFlags();

            Utils.Log(logFile, BackupAction.ScanFolders, "Started.");

            foreach (string masterFolder in this.mediaBackup.MasterFolders)
            {
                Utils.Log(logFile, BackupAction.ScanFolders, "Scanning {0}", masterFolder);

                foreach (string indexFolder in this.mediaBackup.IndexFolders)
                {
                    string folderToCheck = Path.Combine(masterFolder, indexFolder);

                    if (Directory.Exists(folderToCheck))
                    {
                        Utils.Log(logFile, "Scanning {0}", folderToCheck);

                        string[] files = Utils.GetFiles(
                            folderToCheck,
                            filters,
                            SearchOption.AllDirectories,
                            FileAttributes.Hidden);

                        foreach (string file in files)
                        {
                            // Log the file we're checking here                           
#if DEBUG
                            Utils.Log(logFile, "Checking {0}", file);
#endif
                            this.EnsureFile(file, masterFolder, indexFolder);
                        }
                    }
                }
            }

            this.mediaBackup.RemoveFilesWithFlag(false, true);

            this.mediaBackup.Save();


            var totalFiles = this.mediaBackup.BackupFiles.Count();

            long b = 0;

            DateTime oldestFileDate = DateTime.Today;
            BackupFile oldestFile = null;

            foreach (var a in this.mediaBackup.BackupFiles)
            {
                b += a.Length;

                if (a.BackupDiskChecked != null)
                {
                    DateTime d = DateTime.Parse(a.BackupDiskChecked);

                    if (d < oldestFileDate)
                    {
                        oldestFileDate = d;
                        oldestFile = a;
                    }
                }
            }

            Utils.Log(logFile, BackupAction.ScanFolders, "{0:n0} files at {1:n0}MB", totalFiles, b / 1024 / 1024);

            if (oldestFile != null)
            {
                Utils.Log(logFile, BackupAction.ScanFolders, "Oldest backup date is {0:n0} days ago at {1} for {2} on {3}", DateTime.Today.Subtract(oldestFileDate).Days, oldestFileDate.ToShortDateString(), oldestFile.GetFileName(), oldestFile.BackupDisk);
            }

            IEnumerable<BackupFile> filesNotOnBackupDisk =
               this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            Utils.Log(logFile, BackupAction.ScanFolders, "{0:n0} files to backup", filesNotOnBackupDisk.Count());

            IEnumerable<BackupFile> filesWithoutDiskChecked =
               this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            Utils.Log(logFile, BackupAction.ScanFolders, "{0:n0} files without DiskChecked set", filesWithoutDiskChecked.Count());
            
            

            Utils.Log(logFile, BackupAction.ScanFolders, "Completed.");
        }

        #endregion

        private void checkDiskAndDeleteButton_Click(object sender, EventArgs e)
        {
             DialogResult answer = MessageBox.Show(
                "Are you sure you want delete any extra files on the backup disk not in our list?",
                "Delete extra files",
                MessageBoxButtons.YesNo);

             if (answer == DialogResult.Yes)
             {
                 this.CheckConnectedDisk(true);
             }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string LogFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_FilesWithoutBackupDiskChecked.txt");

            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "'{0}' does not have BackupDiskChecked set on backup disk {1} so clearing the BackupDisk.",
                    file.FullPath,
                    file.BackupDisk);

                file.BackupDisk = null;
            }

            this.mediaBackup.Save();
        }

        private void timerButton_Click(object sender, EventArgs e)
        {
            // Start Timer
            backupTimer.Interval = Convert.ToInt32(timerTextBox.Text) * 60 * 1000;
           
            if (timerButton.Text == "Start timer")
            {
                backupTimer.Start();
                timerButton.Text = "Stop timer";

                // Fire once as you've clicked the button
                ScheduledBackup();

            }
            else
            {
                backupTimer.Stop();
                timerButton.Text = "Start timer";
            }
        }

        private void backupTimer_Tick(object sender, EventArgs e)
        {
            ScheduledBackup();
        }

        private void ScheduledBackup()
        {
            string LogFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_ScheduledBackup.txt");

            try
            {
                backupTimer.Stop();

                // Update the master file
                this.ScanFolders();

                // checks for backup disks not verified in > 90 days
                this.CheckForOldBackupDisks();

                // Check the connected backup disk (removing any extra files we dont need)
                this.CheckConnectedDisk(true);

                // Copy any files that need a backup
                this.CopyFiles();

                backupTimer.Start();
            }
            catch (Exception ex)
            {

                Utils.Log(
                    LogFile, BackupAction.General,
                    "Exception occured {0}",
                    ex.Message);
            }

            finally
            {
                backupTimer.Start();
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            string LogFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_ListFilesOnBackupDisk.txt");

            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => p.BackupDisk == this.listFilesTextBox.Text);

            Utils.Log(
                    LogFile,
                    "Listing files on backup disk {0}",
                    this.listFilesTextBox.Text);

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "{0}",
                    file.FullPath);
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            string LogFile = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
              "backup_ListFilesInMasterFolder.txt");

            string masterFolder = this.masterFoldersComboBox.SelectedItem.ToString();

            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => p.MasterFolder == masterFolder);

            Utils.Log(
                    LogFile,
                    "Listing files in master folder {0}",
                   masterFolder);

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "{0} : {1}",
                    file.FullPath, file.BackupDisk);
            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            CheckForOldBackupDisks();
        }

        private void CheckForOldBackupDisks()
        {
            string LogFile = Path.Combine(
             Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
             "backup_ListFilesWithBackupNotCheckedIn90days.txt");

            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => p.BackupDiskChecked != null && DateTime.Parse(p.BackupDiskChecked).AddDays(90) < DateTime.Today);

            IEnumerable<BackupFile> disks = files.GroupBy(p => p.BackupDisk).Select(p=> p.First());

            foreach (BackupFile disk in disks)
            {
                Utils.Log(
                        LogFile, BackupAction.CheckBackupDisk,
                        "Backup disks not checked in 90 days - {0}", disk.BackupDisk);
            }

            Utils.Log(
                    LogFile,
                    "Listing files not checked in 90 days");

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "{0} - not checked in {1} days on disk {2}",
                    file.FullPath, DateTime.Today.Subtract(DateTime.Parse(file.BackupDiskChecked)).Days, file.BackupDisk);
            }
        }
    }
}
