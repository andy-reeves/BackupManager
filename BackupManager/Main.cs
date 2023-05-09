
namespace BackupManager
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Windows.Forms;
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
            this.backupDiskTextBox.Text = Path.Combine(@"\\", System.Environment.MachineName, "backup");

#endif

            
            foreach (string a in this.mediaBackup.MasterFolders)
            {
                this.masterFoldersComboBox.Items.Add(a);
            }

            foreach (string a in this.mediaBackup.MasterFolders)
            {
                this.restoreMasterFolderComboBox.Items.Add(a);
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

        private static void CheckHashOnBackupDisk(string pushoverUserKey, string pushoverAppToken,
            string logFile,
            string fullPath,
            BackupFile backupFile,
            string driveName)
        {
            string hashFromFile = Utils.GetShortMd5HashFromFile(fullPath);

            if (hashFromFile == Utils.ZeroByteHash)
            {
                Utils.LogWithPushover(pushoverUserKey, pushoverAppToken, logFile, BackupAction.CheckBackupDisk, "ERROR: {0} has zerobyte hashcode", fullPath);
            }


            backupFile.BackupDisk = driveName;

            if (hashFromFile == backupFile.ContentsHash)
            {
                // Hash matches so update backup checked date
                backupFile.BackupDiskChecked = DateTime.Now.ToString("yyyy-MM-dd");
            }
            else
            {
                Utils.LogWithPushover(pushoverUserKey, pushoverAppToken, logFile, BackupAction.CheckBackupDisk, "ERROR: {0} has incorrect hashcode", fullPath);
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

            Utils.Log(logFile, "CheckBackupDisk Started.");

            string backupShare = this.backupDiskTextBox.Text;

            // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
            // if not thats a problem
            if (!this.CheckForValidBackupShare(backupShare))
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "No connected backup disk detected.");
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

            long diskFreeSpace = Utils.GetDiskFreeSpace(folderToCheck);
            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Checking {0} - {1}GB free", backupFolderName, diskFreeSpace);

            if (diskFreeSpace < this.mediaBackup.MinimumCriticalBackupDiskSpace)
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk,PushoverPriority.High, "{0}GB free is very low. Prepare new backup disk.", diskFreeSpace);
            }

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
                    CheckHashOnBackupDisk(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, backupFileFullPath, backupFile, backupFolderName);

                    // So we can get the situation where the hash of a file on disk is equal to a hash of a file we have
                    // It could have a different filename on the backup disk though (if we renamed the master file)
                    // check the filenames are equal and rename on the backup disk if they're not

                    string backupDiskFilename = backupFileFullPath.Substring(folderToCheck.Length + 1); // trim off the unc and backup disk parts

                    string masterFilename = Path.Combine(backupFile.IndexFolder, backupFile.RelativePath);

                    if (!backupDiskFilename.Equals(masterFilename))
                    {
                        string destinationFileName = Path.Combine(folderToCheck, backupFile.IndexFolder, backupFile.RelativePath);

                        Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Renaming {0} to {1}", backupFileFullPath, destinationFileName);

                        File.Move(backupFileFullPath, destinationFileName);
                    }
                }
                else
                {
                    // Extra file on a backup disk
                    if (deleteExtraFiles)
                    {
                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken,
                            logFile, BackupAction.CheckBackupDisk,
                            "Extra file {0} on backup disk {1} now deleted.",
                            backupFileFullPath,
                            backupFolderName);

                        Utils.ClearFileAttribute(backupFileFullPath, FileAttributes.ReadOnly);

                        File.Delete(backupFileFullPath);

                    }
                    else
                    {
                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken,
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

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Completed.");
        }

        private void DeleteEmptyDirectories(string logFile, string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                DeleteEmptyDirectories(logFile, directory);
                if (Directory.GetFileSystemEntries(directory).Length == 0)
                {
                    Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Deleting empty folder {0}", directory);
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

            if (!c.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
            {
                MessageBox.Show("Folder in the backup share should start with 'backup ' and then a space and a 1 or 2 digit number.", "Backup Manager", MessageBoxButtons.OK);
                return null;
            }

            return c.Name;
        }

        private bool EnsureConnectedBackupDisk(string backupDisk)
        {
            // checks the specified backup disk is connected already and returns if it is
            //if its not it prompts the user to insert correct disk and waits
            // user clicks 'Yes' inserted and then returns

            if (!CheckForValidBackupShare(this.backupDiskTextBox.Text))
            {
                return false;
            }

            string currentConnectedBackupDiskName = this.GetBackupFolderName(this.backupDiskTextBox.Text);
            while (currentConnectedBackupDiskName != backupDisk)
            {
                DialogResult answer =
            MessageBox.Show(
                String.Format("Please connect backup disk {0} so we can continue restoring files. Have you connected this disk now?", backupDisk),
                "Connect correct backup disk",
                MessageBoxButtons.YesNo);
                if (answer == DialogResult.No)
                {
                    return false;
                }

                if (answer == DialogResult.Yes)
                {
                    currentConnectedBackupDiskName = this.GetBackupFolderName(this.backupDiskTextBox.Text);
                }
            }

            return true;
        }

        private bool CheckForValidBackupShare(string sharePath)
        {
            var a = new DirectoryInfo(sharePath);

            if (a == null || !a.Exists)
            {
                MessageBox.Show("Backup share not found.", "Backup Manager", MessageBoxButtons.OK);
                return false;
            }

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

            if (!c.Name.StartsWith("backup ", StringComparison.CurrentCultureIgnoreCase))
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

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "Started.");

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
                        CheckHashOnBackupDisk(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, destinationFileName, backupFile, backupFolderName);
                    }
                    else
                    {
                        long availableSpace;

                        var b = Utils.GetDiskFreeSpace(backupShare, out availableSpace);

                        if (availableSpace > (this.mediaBackup.MinimumFreeSpaceToLeaveOnBackupDrive *1024*1024))
                        {
                            if (availableSpace > sourceFileInfo.Length)
                            {
                                outOfDiskSpaceMessageSent = false;

                                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "Copying {0}", sourceFileName);
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                                File.Copy(sourceFileName, destinationFileName);

                                // Make sure its not readonly
                                Utils.ClearFileAttribute(destinationFileName, FileAttributes.ReadOnly);

                                Utils.Log(logFile, "Copy complete.");

                                // it could be that the source file hash changed after we read it (we read the hash, updated the master file and then copied it)
                                // in which case check the source hash again and then check the copied file 
                                backupFile.ContentsHash = Utils.GetShortMd5HashFromFile(sourceFileName);
                                CheckHashOnBackupDisk(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, destinationFileName, backupFile, backupFolderName);
                            }
                            else
                            {
                                if (!outOfDiskSpaceMessageSent)
                                {
                                    Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "Skipping {0} as not enough free space", sourceFileName);
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
                    Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "IOException during copy. Skipping file.");
                }
            }

            this.mediaBackup.Save();

            IEnumerable<BackupFile> filesNotOnBackupDisk = this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            if (filesNotOnBackupDisk.Count() > 0)
            {
                long sizeOfFiles = 0;
                foreach (BackupFile file in filesNotOnBackupDisk)
                {   
                    sizeOfFiles += file.Length;
                }

                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0:n0} files still to backup with a size of {1:n0}GB", filesNotOnBackupDisk.Count(), sizeOfFiles/1024/1024/1024);
            }

            IEnumerable<BackupFile> filesWithoutDiskChecked = this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            if (filesWithoutDiskChecked.Count() > 0)
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0:n0} files still without DiskChecked set", filesWithoutDiskChecked.Count());
            }

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.BackupFiles, "Completed.");
        }

        private void EnsureFile(string path, string masterFolder, string indexFolder)
        {
            // Empty Index folder is now no longer allowed
            if (string.IsNullOrEmpty(indexFolder))
            {
                throw new ApplicationException("IndexFolder is empty. Not supported.");
            }

            // if we start with underscore then exclude it
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

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Started.");

            foreach (string masterFolder in this.mediaBackup.MasterFolders)
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Scanning {0}", masterFolder);

                if (Directory.Exists(masterFolder))
                {
                    // Get the Freespace in GB
                    long freeSpaceOnCurrentMasterFolder = Utils.GetDiskFreeSpace(masterFolder);
                    Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0}GB free on {1}", freeSpaceOnCurrentMasterFolder, masterFolder);

                    if (freeSpaceOnCurrentMasterFolder < this.mediaBackup.MinimumCriticalMasterFolderSpace)
                    {
                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Free space on {0} is too low", masterFolder);
                    }

                    foreach (string indexFolder in this.mediaBackup.IndexFolders)
                    {
                        string folderToCheck = Path.Combine(masterFolder, indexFolder);

                        if (Directory.Exists(folderToCheck))
                        {
                            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Scanning {0}", folderToCheck);

                            string[] files = Utils.GetFiles(
                                folderToCheck,
                                filters,
                                SearchOption.AllDirectories,
                                FileAttributes.Hidden);

                            foreach (string file in files)
                            {
#if DEBUG
                                // Log the file we're checking here    
                                Utils.Log(logFile, "Checking {0}", file);
#endif
                                // Checks for TV only
                                if (file.Contains("_TV Series"))
                                {
                                    if (!file.Contains("tvdb") && !file.Contains("tmdb"))
                                    {
                                        //Utils.Log(logFile, "INFO: TV Series has missing tvdb-/tmdb- in the filepath {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "TV Series has missing tvdb-/tmdb- in the filepath {0}", file);
                                    }
                                }

                                // Checks for Movies only
                                if (file.Contains("_Movies"))
                                {
                                    if (!file.Contains("tmdb"))
                                    {
                                        if (!(file.Contains("-featurette.") ||
                                            file.Contains("-other.") ||
                                            file.Contains("-interview.") ||
                                            file.Contains("-scene.") ||
                                            file.Contains("-short.") ||
                                            file.Contains("-deleted.") ||
                                            file.Contains("-behindthescenes.") ||
                                            file.Contains("-trailer.")))
                                            //Utils.Log(logFile, "INFO: Movie has missing tmdb- in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Movie has missing tmdb- in the filename {0}", file);
                                    }
                                }

                                // Checks for Movies, TV, Comedy or Concerts (Video files)
                                if (file.Contains("_TV Series") ||
                                    file.Contains("_Movies") ||
                                    file.Contains("_Concerts") ||
                                    file.Contains("_Comedy"))
                                {
                                    if (file.Contains("subtitles"))
                                    {
                                        //Utils.Log(logFile, "INFO: Video has 'subtitles' in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Video has 'subtitles' in the filename {0}", file);
                                    }

                                    if (file.Contains(" ()"))
                                    {
                                        //Utils.Log(logFile, "INFO: Video has a missing year in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Video has a missing year in the filename {0}", file);
                                    }

                                    if (file.Contains(" (0)"))
                                    {
                                        //Utils.Log(logFile, "INFO: Video has a '0' year in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Video has a '0' year in the filename {0}", file);
                                    }

                                    if (!(file.EndsWith(".mkv") ||
                                       file.EndsWith(".mp4") ||
                                       file.EndsWith(".mpg") ||
                                       file.EndsWith(".mpeg") ||
                                       file.EndsWith(".ts") ||
                                       file.EndsWith(".srt") ||
                                       file.EndsWith(".avi")))
                                    {
                                        //Utils.Log(logFile, "INFO: Video has an invalid file extension in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "Video has an invalid file extension in the filename {0}", file);
                                    }

                                    //Edition checks '{edition-EXTENDED EDITION}'
                                    if (file.Contains("{edition-"))
                                    {
                                        bool found = false;
                                        foreach(string s in this.mediaBackup.EditionsAllowed)
                                        {
                                            if (file.Contains("{edition-" + s, StringComparison.OrdinalIgnoreCase))
                                            {
                                                found = true;
                                                break;
                                            }
                                        }
                                        if (!found)
                                        {
                                           // Utils.Log(logFile, "INFO: File has '{edition-' in the filename {0} but no valid edition specification", file);
                                            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "File has 'edition-' in the filename {0} but no valid edition specification", file);
                                        }
                                    }
                                }

                                // All files except Backup
                                if (!file.Contains("_Backup"))
                                {
                                    if (file.Contains("._"))
                                    {
                                        //Utils.Log(logFile, "INFO: File has '._' in the filename {0}", file);
                                        Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "File has '._' in the filename {0}", file);
                                    }
                                }

                                this.EnsureFile(file, masterFolder, indexFolder);
                            }
                        }
                    }
                }
                else
                {
                    Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, PushoverPriority.High, "{0} doesn't exist", masterFolder);
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

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0:n0} files at {1:n0}MB", totalFiles, b / 1024 / 1024);

            if (oldestFile != null)
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Oldest backup date is {0:n0} days ago at {1} for {2} on {3}", DateTime.Today.Subtract(oldestFileDate).Days, oldestFileDate.ToShortDateString(), oldestFile.GetFileName(), oldestFile.BackupDisk);
            }

            IEnumerable<BackupFile> filesNotOnBackupDisk =
               this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDisk));

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0:n0} files to backup", filesNotOnBackupDisk.Count());

            IEnumerable<BackupFile> filesWithoutDiskChecked =
               this.mediaBackup.BackupFiles.Where(p => string.IsNullOrEmpty(p.BackupDiskChecked));

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "{0:n0} files without DiskChecked set", filesWithoutDiskChecked.Count());

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.ScanFolders, "Completed.");
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

                // Take a copy of the current count of files we backup up last time
                // Then ScanFolders
                // If the new file count is less than x% lower then abort
                // This happens if the server running the backup cannot connect to the nas devices sometimes
                // It'll then delete everything off the connected backup disk as it doesn't think they're needed so this will prevent that

                long oldFileCount = this.mediaBackup.BackupFiles.Count();

                // Update the master file
                this.ScanFolders();

                if (this.mediaBackup.DifferenceInFileCountAllowedPercentage != 0)
                {
                    long minimumFileCountAllowed = oldFileCount - (oldFileCount * this.mediaBackup.DifferenceInFileCountAllowedPercentage / 100);

                    long newFileCount = this.mediaBackup.BackupFiles.Count();

                    if (newFileCount < minimumFileCountAllowed)
                    {
                        throw new Exception("ERROR: The count of files to backup is too low. Check connections to nas drives.");
                    }
                }

                // checks for backup disks not verified in > xx days
                this.CheckForOldBackupDisks();

                // Check the connected backup disk (removing any extra files we dont need)
                this.CheckConnectedDisk(true);

                // Copy any files that need a backup
                this.CopyFiles();

                backupTimer.Start();
            }
            catch (Exception ex)
            {

                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken,
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
             "backup_ListFilesWithBackupNotCheckedInNNdays.txt");

            int numberOfDays = mediaBackup.DaysToReportOldBackupDisks;







            IEnumerable<BackupFile> files =
                this.mediaBackup.BackupFiles.Where(p => p.BackupDiskChecked != null && DateTime.Parse(p.BackupDiskChecked).AddDays(numberOfDays) < DateTime.Today);

            IEnumerable<BackupFile> disks = files.GroupBy(p => p.BackupDisk).Select(p => p.First());

            foreach (BackupFile disk in disks)
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken,
                        LogFile, BackupAction.CheckBackupDisk,
                        "Backup disks not checked in {0} days - {1}", numberOfDays, disk.BackupDisk);
            }

            Utils.Log(
                    LogFile,
                    "Listing files not checked in NN days");

            foreach (BackupFile file in files)
            {
                Utils.Log(
                    LogFile,
                    "{0} - not checked in {1} days on disk {2}",
                    file.FullPath, DateTime.Today.Subtract(DateTime.Parse(file.BackupDiskChecked)).Days, file.BackupDisk);
            }
        }

        private void restoreFilesButton_Click(object sender, EventArgs e)
        {
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
                string LogFile = Path.Combine(
                 Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                 "backup_RestoringBackupDisks.txt");

                if (this.masterFoldersComboBox.SelectedItem == null)
                {
                    MessageBox.Show(
                    "You must select a master folder that you'd files the files from backup disks restored for. This is typically the drive that is now failing.",
                    "Restore backup files",
                    MessageBoxButtons.OK);
                    return;
                }

                string masterFolder = this.masterFoldersComboBox.SelectedItem.ToString();

                if (this.restoreMasterFolderComboBox.SelectedItem == null)
                {
                    MessageBox.Show(
                    "You must select a master folder that you'd files the files from backup copied to. This is typically a new drive that will replace the failing drive.",
                    "Restore backup files",
                    MessageBoxButtons.OK);
                    return;
                }
                string targetMasterFolder = this.restoreMasterFolderComboBox.SelectedItem.ToString();

                if (string.Equals(masterFolder, targetMasterFolder, StringComparison.CurrentCultureIgnoreCase))
                {
                    MessageBox.Show(
                   "You must select 2 different master folders.",
                   "Restore backup files",
                   MessageBoxButtons.OK);
                    return;
                }

                IEnumerable<BackupFile> files =
                    this.mediaBackup.BackupFiles.Where(p => p.MasterFolder == masterFolder && p.BackupDisk != null).OrderBy(q => q.BackupDisk);

                Utils.Log(
                        LogFile,
                        "Restoring files from master folder {0}",
                       masterFolder);

                Utils.Log(
                       LogFile,
                       "Restoring files to target master folder {0}",
                      targetMasterFolder);


                string backupShare = this.backupDiskTextBox.Text;

                foreach (BackupFile file in files)
                {
                    //we  need to check the correct disk is connected and prompt if not
                    if (!EnsureConnectedBackupDisk(file.BackupDisk))
                    {
                        MessageBox.Show(
                  "Cannot connect to the backup drive required.",
                  "Restore backup files",
                  MessageBoxButtons.OK);
                        return;
                    }

                    // calculate the source path
                    // calculate the destination path
                    string sourceFileFullPath = Path.Combine(backupShare, file.BackupDisk, file.IndexFolder, file.RelativePath);

                    string targetFilePath = Path.Combine(targetMasterFolder, file.IndexFolder, file.RelativePath);

                    // log that we're copying the file from the backup disk to the new location

                    Utils.Log(
                        LogFile,
                        "Copying {0} to {1}",
                        sourceFileFullPath, targetFilePath);

                    Utils.EnsureDirectories(targetFilePath);

                    if (!File.Exists(targetFilePath))
                    {
                        File.Copy(sourceFileFullPath, targetFilePath);
                    }

                    if (file.ContentsHash == Utils.GetShortMd5HashFromFile(targetFilePath))
                    {
                        file.MasterFolder = targetMasterFolder;
                    }
                    else
                    {
                        Utils.Log(
                   LogFile,
                   "ERROR: '{0}' already exists in the target but has a different Hashcode", targetFilePath);
                    }
                    // we save everytime so its always correct
                    // all the master folders are different now so save the file
                    this.mediaBackup.Save();
                }
            }
        }

        private void RenameTVButton_Click(object sender, EventArgs e)
        {
            // rename TV backup files

            // Scans the connected backup disk and finds all its files

            string logFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_RenameTVEpisodes.txt");

            Utils.Log(logFile, "Rename TV episodes Started.");

            string backupShare = this.backupDiskTextBox.Text;

            // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
            // if not thats a problem
            if (!this.CheckForValidBackupShare(backupShare))
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "No connected backup disk detected.");
                return;
            }

            string backupFolderName = this.GetBackupFolderName(backupShare);

            string folderToCheck = Path.Combine(backupShare, backupFolderName);

            // reset the filters because we want to search for all extra files
            string filters = "*";

            string[] backupDiskFiles = Utils.GetFiles(
                folderToCheck,
                filters,
                SearchOption.AllDirectories,
                FileAttributes.Hidden);

            foreach (string backupFileFullPath in backupDiskFiles)
            {
                // only the TV files

                if (backupFileFullPath.Contains("\\_TV Series\\"))
                {
                    // find a file in the backup list that starts with this filename
                    // for example if we have 'Nikita s01.e01 Pilot' on disk we want to match 'Nikita s01.e01 Pilot [WebDL-1080p]'
                    string backupDiskFilenameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(backupFileFullPath);

                    string backupDiskFilename = backupFileFullPath.Substring(folderToCheck.Length + 1); // trim off the unc and backup disk parts

                    // Remove the "." back in and trim after sxxexx too

                    // replace ".e" with "e"
                    backupDiskFilenameWithoutExtension = backupDiskFilenameWithoutExtension.Replace(".e", "e");

                    BackupFile backupFile = this.mediaBackup.GetBackupFile(backupDiskFilenameWithoutExtension);

                    // for tv:
                    // Files on disk are like Archer (2009)\Archer (2009) Season 1\Archer (2009) s01.e01 xxxxxxxxx etc
                    // This needs to match with "Archer (2009) s01e01 xxxxxxxx" which has a fullstop removed and other stuff after it now

                    // found a file that matches
                    if (backupFile != null)
                    {
                        if (Path.Combine(backupFile.IndexFolder, backupFile.RelativePath) != backupDiskFilename)
                        {
                            Utils.Log(logFile, "Found a file match {0}", backupFile.FullPath);

                            string destinationFileName = Path.Combine(folderToCheck, backupFile.IndexFolder, backupFile.RelativePath);

                            Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Renaming {0} to {1}", backupFileFullPath, destinationFileName);

                            File.Move(backupFileFullPath, destinationFileName);
                        }
                    }
                }
            }

            this.mediaBackup.Save();

            // Remove all empty folders
            DeleteEmptyDirectories(logFile, folderToCheck);

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Completed.");
        }

        private void renameMoviesButton_Click(object sender, EventArgs e)
        {
            // rename Movie backup files

            // Scans the connected backup disk and finds all its files

            string logFile = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
               "backup_RenameMovies.txt");

            Utils.Log(logFile, "Rename Movies Started.");

            string backupShare = this.backupDiskTextBox.Text;

            // In this shared folder there should be another folder that starts with 'Backup' like 'Backup 18'
            // if not thats a problem
            if (!this.CheckForValidBackupShare(backupShare))
            {
                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "No connected backup disk detected.");
                return;
            }

            string backupFolderName = this.GetBackupFolderName(backupShare);

            string folderToCheck = Path.Combine(backupShare, backupFolderName);

            // reset the filters because we want to search for all extra files
            string filters = "*";

            string[] backupDiskFiles = Utils.GetFiles(
                folderToCheck,
                filters,
                SearchOption.AllDirectories,
                FileAttributes.Hidden);

            foreach (string backupFileFullPath in backupDiskFiles)
            {
                // only the Movie files

                if (backupFileFullPath.Contains("\\_Movies\\"))
                {
                    // find a file in the backup list that starts with this filename
                    // Files on disk are like "Iron Man 3 (2013) xxxxxxxx.mkv
                    // This needs to match with "Iron Man 3 (2013) {tmdb-xxxx} {edition-Directors Cut} xxxxx.mkv"
                    // Ant-Man and the Wasp (2018) {tmdb-363088} [Remux-2160p][PQ][TrueHD Atmos 7.1][h265].mkv
                    string backupDiskFilenameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(backupFileFullPath);

                    string backupDiskFilename = backupFileFullPath.Substring(folderToCheck.Length + 1); // trim off the unc and backup disk parts

                    // replace " - [" with " ["
                    //backupDiskFilenameWithoutExtension = backupDiskFilenameWithoutExtension.Replace(" - [", " [");
                    backupDiskFilenameWithoutExtension = backupDiskFilenameWithoutExtension.SubstringBeforeLast(" - [",StringComparison.InvariantCultureIgnoreCase);

                    BackupFile backupFile = this.mediaBackup.GetBackupFile(backupDiskFilenameWithoutExtension);

                    // found a file that matches
                    if (backupFile != null)
                    {
                        if (Path.Combine(backupFile.IndexFolder, backupFile.RelativePath) != backupDiskFilename)
                        {
                            string destinationFileName = Path.Combine(folderToCheck, backupFile.IndexFolder, backupFile.RelativePath);

                            if (destinationFileName != backupFileFullPath)
                            {
                                Utils.Log(logFile, "Found a file match {0}", backupFile.FullPath);
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationFileName));
                                Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Renaming {0} to {1}", backupFileFullPath, destinationFileName);

                                File.Move(backupFileFullPath, destinationFileName);
                            }
                        }
                    }
                }
            }

            this.mediaBackup.Save();

            // Remove all empty folders
            DeleteEmptyDirectories(logFile, folderToCheck);

            Utils.LogWithPushover(this.mediaBackup.PushoverUserKey, this.mediaBackup.PushoverAppToken, logFile, BackupAction.CheckBackupDisk, "Completed.");
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
              "Are you sure you want rename files and then delete any extra files on the backup disk not in our list?",
              "Delete extra files",
              MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                RenameTVButton_Click(sender, e);
                renameMoviesButton_Click(sender, e);
                this.CheckConnectedDisk(true);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show(
               "Are you sure you want delete any extra files on the backup disk not in our list?",
               "Delete extra files",
               MessageBoxButtons.YesNo);

            if (answer == DialogResult.Yes)
            {
                this.CheckConnectedDisk(true);

                // now copy files
                CopyFiles();
            }
        }
    }
}
