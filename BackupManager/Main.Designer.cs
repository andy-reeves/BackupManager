namespace BackupManager
{
    partial class Main
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Main));
            this.updateMasterFilesButton = new System.Windows.Forms.Button();
            this.checkConnectedBackupDiskButton = new System.Windows.Forms.Button();
            this.copyFilesToBackupDiskButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.backupDiskTextBox = new System.Windows.Forms.TextBox();
            this.listFilesNotOnBackupDiskButton = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.recalculateAllHashesButton = new System.Windows.Forms.Button();
            this.checkDiskAndDeleteButton = new System.Windows.Forms.Button();
            this.checkBackupDeleteAndCopyButton = new System.Windows.Forms.Button();
            this.listMoviesWithMultipleFilesButton = new System.Windows.Forms.Button();
            this.reportBackupDiskStatusButton = new System.Windows.Forms.Button();
            this.speedTestButton = new System.Windows.Forms.Button();
            this.monitoringButton = new System.Windows.Forms.Button();
            this.listFilesWithDuplicateContentHashcodesButton = new System.Windows.Forms.Button();
            this.checkDeleteAndCopyAllBackupDisksButton = new System.Windows.Forms.Button();
            this.checkAllBackupDisksButton = new System.Windows.Forms.Button();
            this.speedTestDisksButton = new System.Windows.Forms.Button();
            this.scheduledBackupTimerButton = new System.Windows.Forms.Button();
            this.listFilesOnBackupDiskButton = new System.Windows.Forms.Button();
            this.listFilesInMasterFolderButton = new System.Windows.Forms.Button();
            this.masterFoldersComboBox = new System.Windows.Forms.ComboBox();
            this.listFilesNotCheckedInXXButton = new System.Windows.Forms.Button();
            this.restoreFilesToMasterFolderButton = new System.Windows.Forms.Button();
            this.restoreMasterFolderComboBox = new System.Windows.Forms.ComboBox();
            this.testPushoverHighButton = new System.Windows.Forms.Button();
            this.testPushoverNormalButton = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.testPushoverEmergencyButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.runOnTimerStartCheckBox = new System.Windows.Forms.CheckBox();
            this.monitoringTimer = new System.Windows.Forms.Timer(this.components);
            this.killProcessesButton = new System.Windows.Forms.Button();
            this.listMasterFoldersComboBox = new System.Windows.Forms.ComboBox();
            this.pushoverGroupBox = new System.Windows.Forms.GroupBox();
            this.pushoverEmergencyCheckBox = new System.Windows.Forms.CheckBox();
            this.pushoverHighCheckBox = new System.Windows.Forms.CheckBox();
            this.pushoverNormalCheckBox = new System.Windows.Forms.CheckBox();
            this.pushoverLowCheckBox = new System.Windows.Forms.CheckBox();
            this.pushoverOnOffButton = new System.Windows.Forms.Button();
            this.testPushoverLowButton = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.listFilesInMasterFolderGroupBox = new System.Windows.Forms.GroupBox();
            this.listFilesOnBackupDiskGroupBox = new System.Windows.Forms.GroupBox();
            this.listFilesComboBox = new System.Windows.Forms.ComboBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.scheduledDateTimePicker = new System.Windows.Forms.DateTimePicker();
            this.stopProcessButton = new System.Windows.Forms.Button();
            this.processesComboBox = new System.Windows.Forms.ComboBox();
            this.processesGroupBox = new System.Windows.Forms.GroupBox();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripProgressBar = new System.Windows.Forms.ToolStripProgressBar();
            this.cancelButton = new System.Windows.Forms.Button();
            this.allBackupDisksGroupBox = new System.Windows.Forms.GroupBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.listFilesGroupBox = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.currentBackupDiskTextBox = new System.Windows.Forms.TextBox();
            this.backupDiskCapacityTextBox = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.backupDiskAvailableTextBox = new System.Windows.Forms.TextBox();
            this.label8 = new System.Windows.Forms.Label();
            this.notOnABackupDiskTextBox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.totalFilesTextBox = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.notOnABackupDiskSizeTextBox1 = new System.Windows.Forms.TextBox();
            this.totalFilesSizeTextBox = new System.Windows.Forms.TextBox();
            this.estimatedFinishTimeTextBox = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.pushoverGroupBox.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.listFilesInMasterFolderGroupBox.SuspendLayout();
            this.listFilesOnBackupDiskGroupBox.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.processesGroupBox.SuspendLayout();
            this.statusStrip.SuspendLayout();
            this.allBackupDisksGroupBox.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.listFilesGroupBox.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.SuspendLayout();
            // 
            // updateMasterFilesButton
            // 
            this.updateMasterFilesButton.Location = new System.Drawing.Point(7, 22);
            this.updateMasterFilesButton.Name = "updateMasterFilesButton";
            this.updateMasterFilesButton.Size = new System.Drawing.Size(201, 23);
            this.updateMasterFilesButton.TabIndex = 0;
            this.updateMasterFilesButton.Text = "Update file list";
            this.toolTip.SetToolTip(this.updateMasterFilesButton, "Resets the entire collection of backup files. Extra entries are removed if no lon" +
        "ger there.");
            this.updateMasterFilesButton.UseVisualStyleBackColor = true;
            this.updateMasterFilesButton.Click += new System.EventHandler(this.UpdateMasterFilesButton_Click);
            // 
            // checkConnectedBackupDiskButton
            // 
            this.checkConnectedBackupDiskButton.Location = new System.Drawing.Point(6, 22);
            this.checkConnectedBackupDiskButton.Name = "checkConnectedBackupDiskButton";
            this.checkConnectedBackupDiskButton.Size = new System.Drawing.Size(201, 23);
            this.checkConnectedBackupDiskButton.TabIndex = 1;
            this.checkConnectedBackupDiskButton.Text = "Check (don\'t remove extra files)";
            this.toolTip.SetToolTip(this.checkConnectedBackupDiskButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.checkConnectedBackupDiskButton.UseVisualStyleBackColor = true;
            this.checkConnectedBackupDiskButton.Click += new System.EventHandler(this.CheckConnectedBackupDiskButton_Click);
            // 
            // copyFilesToBackupDiskButton
            // 
            this.copyFilesToBackupDiskButton.Location = new System.Drawing.Point(6, 78);
            this.copyFilesToBackupDiskButton.Name = "copyFilesToBackupDiskButton";
            this.copyFilesToBackupDiskButton.Size = new System.Drawing.Size(201, 23);
            this.copyFilesToBackupDiskButton.TabIndex = 5;
            this.copyFilesToBackupDiskButton.Text = "Copy files from master";
            this.toolTip.SetToolTip(this.copyFilesToBackupDiskButton, "Copies any files without a Backup disk set to a connected Backup drive.");
            this.copyFilesToBackupDiskButton.UseVisualStyleBackColor = true;
            this.copyFilesToBackupDiskButton.Click += new System.EventHandler(this.CopyFilesToBackupDiskButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(62, 15);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Backup drive";
            // 
            // backupDiskTextBox
            // 
            this.backupDiskTextBox.Location = new System.Drawing.Point(140, 12);
            this.backupDiskTextBox.Name = "backupDiskTextBox";
            this.backupDiskTextBox.Size = new System.Drawing.Size(106, 20);
            this.backupDiskTextBox.TabIndex = 7;
            this.backupDiskTextBox.Text = "d:\\";
            // 
            // listFilesNotOnBackupDiskButton
            // 
            this.listFilesNotOnBackupDiskButton.Location = new System.Drawing.Point(6, 20);
            this.listFilesNotOnBackupDiskButton.Name = "listFilesNotOnBackupDiskButton";
            this.listFilesNotOnBackupDiskButton.Size = new System.Drawing.Size(199, 23);
            this.listFilesNotOnBackupDiskButton.TabIndex = 11;
            this.listFilesNotOnBackupDiskButton.Text = "... not on a Backup disk";
            this.toolTip.SetToolTip(this.listFilesNotOnBackupDiskButton, "Outputs files that are not yet on a Backup drive.");
            this.listFilesNotOnBackupDiskButton.UseVisualStyleBackColor = true;
            this.listFilesNotOnBackupDiskButton.Click += new System.EventHandler(this.ListFilesNotOnBackupDiskButton_Click);
            // 
            // recalculateAllHashesButton
            // 
            this.recalculateAllHashesButton.Location = new System.Drawing.Point(7, 79);
            this.recalculateAllHashesButton.Name = "recalculateAllHashesButton";
            this.recalculateAllHashesButton.Size = new System.Drawing.Size(201, 23);
            this.recalculateAllHashesButton.TabIndex = 16;
            this.recalculateAllHashesButton.Text = "Recalculate all Hashes";
            this.toolTip.SetToolTip(this.recalculateAllHashesButton, "Recalculates all the Hashcodes from the Master Files. Only use this is the hash a" +
        "lgorithm has been changed.");
            this.recalculateAllHashesButton.UseVisualStyleBackColor = true;
            this.recalculateAllHashesButton.Click += new System.EventHandler(this.RecalculateAllHashesButton_Click);
            // 
            // checkDiskAndDeleteButton
            // 
            this.checkDiskAndDeleteButton.Location = new System.Drawing.Point(6, 50);
            this.checkDiskAndDeleteButton.Name = "checkDiskAndDeleteButton";
            this.checkDiskAndDeleteButton.Size = new System.Drawing.Size(201, 23);
            this.checkDiskAndDeleteButton.TabIndex = 19;
            this.checkDiskAndDeleteButton.Text = "Check (and remove extra files)";
            this.toolTip.SetToolTip(this.checkDiskAndDeleteButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkDiskAndDeleteButton.UseVisualStyleBackColor = true;
            this.checkDiskAndDeleteButton.Click += new System.EventHandler(this.CheckDiskAndDeleteButton_Click);
            // 
            // checkBackupDeleteAndCopyButton
            // 
            this.checkBackupDeleteAndCopyButton.Location = new System.Drawing.Point(6, 105);
            this.checkBackupDeleteAndCopyButton.Name = "checkBackupDeleteAndCopyButton";
            this.checkBackupDeleteAndCopyButton.Size = new System.Drawing.Size(201, 23);
            this.checkBackupDeleteAndCopyButton.TabIndex = 36;
            this.checkBackupDeleteAndCopyButton.Text = "Check, remove extra files and copy";
            this.toolTip.SetToolTip(this.checkBackupDeleteAndCopyButton, "Checks a connected backup disk and then copies files.\r\nSets the BackupDisk and th" +
        "e BackupDiskChecked (if the HashCode is correct). DELETES FILES.");
            this.checkBackupDeleteAndCopyButton.UseVisualStyleBackColor = true;
            this.checkBackupDeleteAndCopyButton.Click += new System.EventHandler(this.CheckBackupDeleteAndCopyButton_Click);
            // 
            // listMoviesWithMultipleFilesButton
            // 
            this.listMoviesWithMultipleFilesButton.Location = new System.Drawing.Point(6, 75);
            this.listMoviesWithMultipleFilesButton.Name = "listMoviesWithMultipleFilesButton";
            this.listMoviesWithMultipleFilesButton.Size = new System.Drawing.Size(199, 23);
            this.listMoviesWithMultipleFilesButton.TabIndex = 37;
            this.listMoviesWithMultipleFilesButton.Text = "... with multiple movie files";
            this.toolTip.SetToolTip(this.listMoviesWithMultipleFilesButton, "Lists any movies that have multiple video files in the same folder");
            this.listMoviesWithMultipleFilesButton.UseVisualStyleBackColor = true;
            this.listMoviesWithMultipleFilesButton.Click += new System.EventHandler(this.ListMoviesWithMultipleFilesButton_Click);
            // 
            // reportBackupDiskStatusButton
            // 
            this.reportBackupDiskStatusButton.Location = new System.Drawing.Point(6, 18);
            this.reportBackupDiskStatusButton.Name = "reportBackupDiskStatusButton";
            this.reportBackupDiskStatusButton.Size = new System.Drawing.Size(201, 23);
            this.reportBackupDiskStatusButton.TabIndex = 43;
            this.reportBackupDiskStatusButton.Text = "List disk status";
            this.toolTip.SetToolTip(this.reportBackupDiskStatusButton, "Reports the status of each backup disk");
            this.reportBackupDiskStatusButton.UseVisualStyleBackColor = true;
            this.reportBackupDiskStatusButton.Click += new System.EventHandler(this.ReportBackupDiskStatusButton_Click);
            // 
            // speedTestButton
            // 
            this.speedTestButton.Location = new System.Drawing.Point(7, 50);
            this.speedTestButton.Name = "speedTestButton";
            this.speedTestButton.Size = new System.Drawing.Size(201, 23);
            this.speedTestButton.TabIndex = 44;
            this.speedTestButton.Text = "Speed test";
            this.toolTip.SetToolTip(this.speedTestButton, "Runs the speed test on all master folders");
            this.speedTestButton.UseVisualStyleBackColor = true;
            this.speedTestButton.Click += new System.EventHandler(this.SpeedTestButton_Click);
            // 
            // monitoringButton
            // 
            this.monitoringButton.Location = new System.Drawing.Point(107, 86);
            this.monitoringButton.Name = "monitoringButton";
            this.monitoringButton.Size = new System.Drawing.Size(98, 23);
            this.monitoringButton.TabIndex = 50;
            this.monitoringButton.Text = "Monitoring = OFF";
            this.toolTip.SetToolTip(this.monitoringButton, "Starts the service monitoring");
            this.monitoringButton.UseVisualStyleBackColor = true;
            this.monitoringButton.Click += new System.EventHandler(this.MonitoringButton_Click);
            // 
            // listFilesWithDuplicateContentHashcodesButton
            // 
            this.listFilesWithDuplicateContentHashcodesButton.Location = new System.Drawing.Point(6, 104);
            this.listFilesWithDuplicateContentHashcodesButton.Name = "listFilesWithDuplicateContentHashcodesButton";
            this.listFilesWithDuplicateContentHashcodesButton.Size = new System.Drawing.Size(199, 23);
            this.listFilesWithDuplicateContentHashcodesButton.TabIndex = 61;
            this.listFilesWithDuplicateContentHashcodesButton.Text = "... with duplicate content hashcodes";
            this.toolTip.SetToolTip(this.listFilesWithDuplicateContentHashcodesButton, "List files with duplicate content hashcodes");
            this.listFilesWithDuplicateContentHashcodesButton.UseVisualStyleBackColor = true;
            this.listFilesWithDuplicateContentHashcodesButton.Click += new System.EventHandler(this.ListFilesWithDuplicateContentHashcodesButton_Click);
            // 
            // checkDeleteAndCopyAllBackupDisksButton
            // 
            this.checkDeleteAndCopyAllBackupDisksButton.Location = new System.Drawing.Point(6, 77);
            this.checkDeleteAndCopyAllBackupDisksButton.Name = "checkDeleteAndCopyAllBackupDisksButton";
            this.checkDeleteAndCopyAllBackupDisksButton.Size = new System.Drawing.Size(201, 23);
            this.checkDeleteAndCopyAllBackupDisksButton.TabIndex = 63;
            this.checkDeleteAndCopyAllBackupDisksButton.Text = "Check, remove extra files and copy";
            this.toolTip.SetToolTip(this.checkDeleteAndCopyAllBackupDisksButton, "Checks a connected backup disk and copies files. Then waits for the next disk to " +
        "be connected. \r\nSets the BackupDisk and the BackupDiskChecked (if the HashCode i" +
        "s correct). DELETES FILES.");
            this.checkDeleteAndCopyAllBackupDisksButton.UseVisualStyleBackColor = true;
            this.checkDeleteAndCopyAllBackupDisksButton.Click += new System.EventHandler(this.CheckDeleteAndCopyAllBackupDisksButton_Click);
            // 
            // checkAllBackupDisksButton
            // 
            this.checkAllBackupDisksButton.Location = new System.Drawing.Point(6, 47);
            this.checkAllBackupDisksButton.Name = "checkAllBackupDisksButton";
            this.checkAllBackupDisksButton.Size = new System.Drawing.Size(201, 23);
            this.checkAllBackupDisksButton.TabIndex = 65;
            this.checkAllBackupDisksButton.Text = "Check (and remove extra files)";
            this.toolTip.SetToolTip(this.checkAllBackupDisksButton, "Checks a connected backup disk. Then waits for the next disk to be connected. \r\nS" +
        "ets the BackupDisk and the BackupDiskChecked (if the HashCode is correct). DELET" +
        "ES FILES.");
            this.checkAllBackupDisksButton.UseVisualStyleBackColor = true;
            this.checkAllBackupDisksButton.Click += new System.EventHandler(this.CheckAllBackupDisksButton_Click);
            // 
            // speedTestDisksButton
            // 
            this.speedTestDisksButton.Location = new System.Drawing.Point(817, 259);
            this.speedTestDisksButton.Name = "speedTestDisksButton";
            this.speedTestDisksButton.Size = new System.Drawing.Size(199, 23);
            this.speedTestDisksButton.TabIndex = 60;
            this.speedTestDisksButton.Text = "Speed Test Disks = OFF";
            this.toolTip.SetToolTip(this.speedTestDisksButton, "Starts the service monitoring");
            this.speedTestDisksButton.UseVisualStyleBackColor = true;
            this.speedTestDisksButton.Click += new System.EventHandler(this.SpeedTestDisksButton_Click);
            // 
            // scheduledBackupTimerButton
            // 
            this.scheduledBackupTimerButton.Location = new System.Drawing.Point(112, 55);
            this.scheduledBackupTimerButton.Name = "scheduledBackupTimerButton";
            this.scheduledBackupTimerButton.Size = new System.Drawing.Size(98, 23);
            this.scheduledBackupTimerButton.TabIndex = 21;
            this.scheduledBackupTimerButton.Text = "Backup = OFF";
            this.scheduledBackupTimerButton.UseVisualStyleBackColor = true;
            this.scheduledBackupTimerButton.Click += new System.EventHandler(this.BackupTimerButton_Click);
            // 
            // listFilesOnBackupDiskButton
            // 
            this.listFilesOnBackupDiskButton.Location = new System.Drawing.Point(138, 24);
            this.listFilesOnBackupDiskButton.Name = "listFilesOnBackupDiskButton";
            this.listFilesOnBackupDiskButton.Size = new System.Drawing.Size(69, 23);
            this.listFilesOnBackupDiskButton.TabIndex = 24;
            this.listFilesOnBackupDiskButton.Text = "List";
            this.listFilesOnBackupDiskButton.UseVisualStyleBackColor = true;
            this.listFilesOnBackupDiskButton.Click += new System.EventHandler(this.ListFilesOnBackupDiskButton_Click);
            // 
            // listFilesInMasterFolderButton
            // 
            this.listFilesInMasterFolderButton.Location = new System.Drawing.Point(138, 19);
            this.listFilesInMasterFolderButton.Name = "listFilesInMasterFolderButton";
            this.listFilesInMasterFolderButton.Size = new System.Drawing.Size(69, 23);
            this.listFilesInMasterFolderButton.TabIndex = 26;
            this.listFilesInMasterFolderButton.Text = "List";
            this.listFilesInMasterFolderButton.UseVisualStyleBackColor = true;
            this.listFilesInMasterFolderButton.Click += new System.EventHandler(this.ListFilesInMasterFolderButton_Click);
            // 
            // masterFoldersComboBox
            // 
            this.masterFoldersComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.masterFoldersComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.masterFoldersComboBox.FormattingEnabled = true;
            this.masterFoldersComboBox.Location = new System.Drawing.Point(102, 20);
            this.masterFoldersComboBox.Name = "masterFoldersComboBox";
            this.masterFoldersComboBox.Size = new System.Drawing.Size(105, 21);
            this.masterFoldersComboBox.TabIndex = 29;
            // 
            // listFilesNotCheckedInXXButton
            // 
            this.listFilesNotCheckedInXXButton.Location = new System.Drawing.Point(6, 47);
            this.listFilesNotCheckedInXXButton.Name = "listFilesNotCheckedInXXButton";
            this.listFilesNotCheckedInXXButton.Size = new System.Drawing.Size(199, 23);
            this.listFilesNotCheckedInXXButton.TabIndex = 30;
            this.listFilesNotCheckedInXXButton.Text = "... on old Backup disks";
            this.listFilesNotCheckedInXXButton.UseVisualStyleBackColor = true;
            this.listFilesNotCheckedInXXButton.Click += new System.EventHandler(this.CheckForOldBackupDisks_Click);
            // 
            // restoreFilesToMasterFolderButton
            // 
            this.restoreFilesToMasterFolderButton.Location = new System.Drawing.Point(138, 73);
            this.restoreFilesToMasterFolderButton.Name = "restoreFilesToMasterFolderButton";
            this.restoreFilesToMasterFolderButton.Size = new System.Drawing.Size(69, 23);
            this.restoreFilesToMasterFolderButton.TabIndex = 31;
            this.restoreFilesToMasterFolderButton.Text = "Restore";
            this.restoreFilesToMasterFolderButton.UseVisualStyleBackColor = true;
            this.restoreFilesToMasterFolderButton.Click += new System.EventHandler(this.RestoreFilesButton_Click);
            // 
            // restoreMasterFolderComboBox
            // 
            this.restoreMasterFolderComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.restoreMasterFolderComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.restoreMasterFolderComboBox.FormattingEnabled = true;
            this.restoreMasterFolderComboBox.Location = new System.Drawing.Point(102, 46);
            this.restoreMasterFolderComboBox.Name = "restoreMasterFolderComboBox";
            this.restoreMasterFolderComboBox.Size = new System.Drawing.Size(105, 21);
            this.restoreMasterFolderComboBox.TabIndex = 32;
            // 
            // testPushoverHighButton
            // 
            this.testPushoverHighButton.Location = new System.Drawing.Point(9, 72);
            this.testPushoverHighButton.Name = "testPushoverHighButton";
            this.testPushoverHighButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverHighButton.TabIndex = 38;
            this.testPushoverHighButton.Text = "High";
            this.testPushoverHighButton.UseVisualStyleBackColor = true;
            this.testPushoverHighButton.Click += new System.EventHandler(this.TestPushoverHighButton_Click);
            // 
            // testPushoverNormalButton
            // 
            this.testPushoverNormalButton.Location = new System.Drawing.Point(9, 45);
            this.testPushoverNormalButton.Name = "testPushoverNormalButton";
            this.testPushoverNormalButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverNormalButton.TabIndex = 39;
            this.testPushoverNormalButton.Text = "Normal";
            this.testPushoverNormalButton.UseVisualStyleBackColor = true;
            this.testPushoverNormalButton.Click += new System.EventHandler(this.TestPushoverNormalButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 23);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(57, 13);
            this.label3.TabIndex = 40;
            this.label3.Text = "Src master";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 49);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(63, 13);
            this.label4.TabIndex = 41;
            this.label4.Text = "Dest master";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // testPushoverEmergencyButton
            // 
            this.testPushoverEmergencyButton.Location = new System.Drawing.Point(9, 100);
            this.testPushoverEmergencyButton.Name = "testPushoverEmergencyButton";
            this.testPushoverEmergencyButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverEmergencyButton.TabIndex = 42;
            this.testPushoverEmergencyButton.Text = "Emergency";
            this.testPushoverEmergencyButton.UseVisualStyleBackColor = true;
            this.testPushoverEmergencyButton.Click += new System.EventHandler(this.TestPushoverEmergencyButton_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(2, 31);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 13);
            this.label2.TabIndex = 47;
            this.label2.Text = "Time";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(67, 32);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(10, 13);
            this.label5.TabIndex = 48;
            this.label5.Text = ":";
            this.label5.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // runOnTimerStartCheckBox
            // 
            this.runOnTimerStartCheckBox.AutoSize = true;
            this.runOnTimerStartCheckBox.Checked = true;
            this.runOnTimerStartCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.runOnTimerStartCheckBox.Location = new System.Drawing.Point(5, 59);
            this.runOnTimerStartCheckBox.Name = "runOnTimerStartCheckBox";
            this.runOnTimerStartCheckBox.Size = new System.Drawing.Size(84, 17);
            this.runOnTimerStartCheckBox.TabIndex = 49;
            this.runOnTimerStartCheckBox.Text = "Run on start";
            this.runOnTimerStartCheckBox.UseVisualStyleBackColor = true;
            // 
            // monitoringTimer
            // 
            this.monitoringTimer.Interval = 60000;
            this.monitoringTimer.Tick += new System.EventHandler(this.MonitoringTimer_Tick);
            // 
            // killProcessesButton
            // 
            this.killProcessesButton.Location = new System.Drawing.Point(6, 57);
            this.killProcessesButton.Name = "killProcessesButton";
            this.killProcessesButton.Size = new System.Drawing.Size(199, 23);
            this.killProcessesButton.TabIndex = 51;
            this.killProcessesButton.Text = "Stop all processes";
            this.killProcessesButton.UseVisualStyleBackColor = true;
            this.killProcessesButton.Click += new System.EventHandler(this.KillProcessesButton_Click);
            // 
            // listMasterFoldersComboBox
            // 
            this.listMasterFoldersComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.listMasterFoldersComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.listMasterFoldersComboBox.FormattingEnabled = true;
            this.listMasterFoldersComboBox.Location = new System.Drawing.Point(6, 19);
            this.listMasterFoldersComboBox.Name = "listMasterFoldersComboBox";
            this.listMasterFoldersComboBox.Size = new System.Drawing.Size(126, 21);
            this.listMasterFoldersComboBox.TabIndex = 52;
            // 
            // pushoverGroupBox
            // 
            this.pushoverGroupBox.Controls.Add(this.pushoverEmergencyCheckBox);
            this.pushoverGroupBox.Controls.Add(this.pushoverHighCheckBox);
            this.pushoverGroupBox.Controls.Add(this.pushoverNormalCheckBox);
            this.pushoverGroupBox.Controls.Add(this.pushoverLowCheckBox);
            this.pushoverGroupBox.Controls.Add(this.pushoverOnOffButton);
            this.pushoverGroupBox.Controls.Add(this.testPushoverLowButton);
            this.pushoverGroupBox.Controls.Add(this.testPushoverNormalButton);
            this.pushoverGroupBox.Controls.Add(this.testPushoverHighButton);
            this.pushoverGroupBox.Controls.Add(this.testPushoverEmergencyButton);
            this.pushoverGroupBox.Location = new System.Drawing.Point(1066, 9);
            this.pushoverGroupBox.Name = "pushoverGroupBox";
            this.pushoverGroupBox.Size = new System.Drawing.Size(217, 131);
            this.pushoverGroupBox.TabIndex = 54;
            this.pushoverGroupBox.TabStop = false;
            this.pushoverGroupBox.Text = "Pushover";
            // 
            // pushoverEmergencyCheckBox
            // 
            this.pushoverEmergencyCheckBox.AutoSize = true;
            this.pushoverEmergencyCheckBox.Location = new System.Drawing.Point(83, 105);
            this.pushoverEmergencyCheckBox.Name = "pushoverEmergencyCheckBox";
            this.pushoverEmergencyCheckBox.Size = new System.Drawing.Size(15, 14);
            this.pushoverEmergencyCheckBox.TabIndex = 72;
            this.pushoverEmergencyCheckBox.UseVisualStyleBackColor = true;
            this.pushoverEmergencyCheckBox.CheckedChanged += new System.EventHandler(this.PushoverEmergencyCheckBox_CheckedChanged);
            // 
            // pushoverHighCheckBox
            // 
            this.pushoverHighCheckBox.AutoSize = true;
            this.pushoverHighCheckBox.Location = new System.Drawing.Point(83, 77);
            this.pushoverHighCheckBox.Name = "pushoverHighCheckBox";
            this.pushoverHighCheckBox.Size = new System.Drawing.Size(15, 14);
            this.pushoverHighCheckBox.TabIndex = 71;
            this.pushoverHighCheckBox.UseVisualStyleBackColor = true;
            this.pushoverHighCheckBox.CheckedChanged += new System.EventHandler(this.PushoverHighCheckBox_CheckedChanged);
            // 
            // pushoverNormalCheckBox
            // 
            this.pushoverNormalCheckBox.AutoSize = true;
            this.pushoverNormalCheckBox.Location = new System.Drawing.Point(83, 50);
            this.pushoverNormalCheckBox.Name = "pushoverNormalCheckBox";
            this.pushoverNormalCheckBox.Size = new System.Drawing.Size(15, 14);
            this.pushoverNormalCheckBox.TabIndex = 70;
            this.pushoverNormalCheckBox.UseVisualStyleBackColor = true;
            this.pushoverNormalCheckBox.CheckedChanged += new System.EventHandler(this.PushoverNormalCheckBox_CheckedChanged);
            // 
            // pushoverLowCheckBox
            // 
            this.pushoverLowCheckBox.AutoSize = true;
            this.pushoverLowCheckBox.Location = new System.Drawing.Point(83, 22);
            this.pushoverLowCheckBox.Name = "pushoverLowCheckBox";
            this.pushoverLowCheckBox.Size = new System.Drawing.Size(15, 14);
            this.pushoverLowCheckBox.TabIndex = 69;
            this.pushoverLowCheckBox.UseVisualStyleBackColor = true;
            this.pushoverLowCheckBox.CheckedChanged += new System.EventHandler(this.PushoverLowCheckBox_CheckedChanged);
            // 
            // pushoverOnOffButton
            // 
            this.pushoverOnOffButton.Location = new System.Drawing.Point(112, 58);
            this.pushoverOnOffButton.Name = "pushoverOnOffButton";
            this.pushoverOnOffButton.Size = new System.Drawing.Size(98, 23);
            this.pushoverOnOffButton.TabIndex = 44;
            this.pushoverOnOffButton.Text = "Sending = OFF";
            this.pushoverOnOffButton.UseVisualStyleBackColor = true;
            this.pushoverOnOffButton.Click += new System.EventHandler(this.PushoverOnOffButton_Click);
            // 
            // testPushoverLowButton
            // 
            this.testPushoverLowButton.Location = new System.Drawing.Point(9, 18);
            this.testPushoverLowButton.Name = "testPushoverLowButton";
            this.testPushoverLowButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverLowButton.TabIndex = 43;
            this.testPushoverLowButton.Text = "Low";
            this.testPushoverLowButton.UseVisualStyleBackColor = true;
            this.testPushoverLowButton.Click += new System.EventHandler(this.TestPushoverLowButton_Click);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.restoreFilesToMasterFolderButton);
            this.groupBox2.Controls.Add(this.masterFoldersComboBox);
            this.groupBox2.Controls.Add(this.restoreMasterFolderComboBox);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Location = new System.Drawing.Point(811, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(217, 109);
            this.groupBox2.TabIndex = 55;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Restore from Backup";
            // 
            // listFilesInMasterFolderGroupBox
            // 
            this.listFilesInMasterFolderGroupBox.Controls.Add(this.listFilesInMasterFolderButton);
            this.listFilesInMasterFolderGroupBox.Controls.Add(this.listMasterFoldersComboBox);
            this.listFilesInMasterFolderGroupBox.Location = new System.Drawing.Point(290, 162);
            this.listFilesInMasterFolderGroupBox.Name = "listFilesInMasterFolderGroupBox";
            this.listFilesInMasterFolderGroupBox.Size = new System.Drawing.Size(217, 49);
            this.listFilesInMasterFolderGroupBox.TabIndex = 56;
            this.listFilesInMasterFolderGroupBox.TabStop = false;
            this.listFilesInMasterFolderGroupBox.Text = "List files in master folder";
            // 
            // listFilesOnBackupDiskGroupBox
            // 
            this.listFilesOnBackupDiskGroupBox.Controls.Add(this.listFilesComboBox);
            this.listFilesOnBackupDiskGroupBox.Controls.Add(this.listFilesOnBackupDiskButton);
            this.listFilesOnBackupDiskGroupBox.Location = new System.Drawing.Point(290, 222);
            this.listFilesOnBackupDiskGroupBox.Name = "listFilesOnBackupDiskGroupBox";
            this.listFilesOnBackupDiskGroupBox.Size = new System.Drawing.Size(217, 54);
            this.listFilesOnBackupDiskGroupBox.TabIndex = 57;
            this.listFilesOnBackupDiskGroupBox.TabStop = false;
            this.listFilesOnBackupDiskGroupBox.Text = "List files on Backup disk";
            // 
            // listFilesComboBox
            // 
            this.listFilesComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.listFilesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.listFilesComboBox.FormattingEnabled = true;
            this.listFilesComboBox.Location = new System.Drawing.Point(6, 24);
            this.listFilesComboBox.Name = "listFilesComboBox";
            this.listFilesComboBox.Size = new System.Drawing.Size(126, 21);
            this.listFilesComboBox.TabIndex = 54;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.scheduledDateTimePicker);
            this.groupBox5.Controls.Add(this.scheduledBackupTimerButton);
            this.groupBox5.Controls.Add(this.label2);
            this.groupBox5.Controls.Add(this.label5);
            this.groupBox5.Controls.Add(this.runOnTimerStartCheckBox);
            this.groupBox5.Location = new System.Drawing.Point(1066, 155);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(217, 88);
            this.groupBox5.TabIndex = 58;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Scheduled Backup";
            // 
            // scheduledDateTimePicker
            // 
            this.scheduledDateTimePicker.CustomFormat = "HH:mm";
            this.scheduledDateTimePicker.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            this.scheduledDateTimePicker.Location = new System.Drawing.Point(38, 28);
            this.scheduledDateTimePicker.Name = "scheduledDateTimePicker";
            this.scheduledDateTimePicker.ShowUpDown = true;
            this.scheduledDateTimePicker.Size = new System.Drawing.Size(57, 20);
            this.scheduledDateTimePicker.TabIndex = 69;
            // 
            // stopProcessButton
            // 
            this.stopProcessButton.Location = new System.Drawing.Point(136, 25);
            this.stopProcessButton.Name = "stopProcessButton";
            this.stopProcessButton.Size = new System.Drawing.Size(69, 23);
            this.stopProcessButton.TabIndex = 59;
            this.stopProcessButton.Text = "Stop";
            this.stopProcessButton.UseVisualStyleBackColor = true;
            this.stopProcessButton.Click += new System.EventHandler(this.StopProcessButton_Click);
            // 
            // processesComboBox
            // 
            this.processesComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.processesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.processesComboBox.FormattingEnabled = true;
            this.processesComboBox.Location = new System.Drawing.Point(6, 25);
            this.processesComboBox.Name = "processesComboBox";
            this.processesComboBox.Size = new System.Drawing.Size(108, 21);
            this.processesComboBox.TabIndex = 54;
            // 
            // processesGroupBox
            // 
            this.processesGroupBox.Controls.Add(this.killProcessesButton);
            this.processesGroupBox.Controls.Add(this.stopProcessButton);
            this.processesGroupBox.Controls.Add(this.processesComboBox);
            this.processesGroupBox.Controls.Add(this.monitoringButton);
            this.processesGroupBox.Location = new System.Drawing.Point(811, 134);
            this.processesGroupBox.Name = "processesGroupBox";
            this.processesGroupBox.Size = new System.Drawing.Size(217, 119);
            this.processesGroupBox.TabIndex = 60;
            this.processesGroupBox.TabStop = false;
            this.processesGroupBox.Text = "Processes/Services";
            // 
            // statusStrip
            // 
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabel,
            this.toolStripProgressBar});
            this.statusStrip.Location = new System.Drawing.Point(0, 314);
            this.statusStrip.Name = "statusStrip";
            this.statusStrip.Size = new System.Drawing.Size(1309, 22);
            this.statusStrip.TabIndex = 62;
            this.statusStrip.Text = "statusStrip1";
            // 
            // toolStripStatusLabel
            // 
            this.toolStripStatusLabel.Name = "toolStripStatusLabel";
            this.toolStripStatusLabel.Size = new System.Drawing.Size(1294, 17);
            this.toolStripStatusLabel.Spring = true;
            this.toolStripStatusLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // toolStripProgressBar
            // 
            this.toolStripProgressBar.Name = "toolStripProgressBar";
            this.toolStripProgressBar.Size = new System.Drawing.Size(100, 16);
            this.toolStripProgressBar.Visible = false;
            // 
            // cancelButton
            // 
            this.cancelButton.Enabled = false;
            this.cancelButton.Location = new System.Drawing.Point(1066, 281);
            this.cancelButton.Name = "cancelButton";
            this.cancelButton.Size = new System.Drawing.Size(217, 23);
            this.cancelButton.TabIndex = 64;
            this.cancelButton.Text = "Cancel";
            this.cancelButton.UseVisualStyleBackColor = true;
            this.cancelButton.Click += new System.EventHandler(this.CancelButton_Click);
            // 
            // allBackupDisksGroupBox
            // 
            this.allBackupDisksGroupBox.Controls.Add(this.checkAllBackupDisksButton);
            this.allBackupDisksGroupBox.Controls.Add(this.checkDeleteAndCopyAllBackupDisksButton);
            this.allBackupDisksGroupBox.Controls.Add(this.reportBackupDiskStatusButton);
            this.allBackupDisksGroupBox.Location = new System.Drawing.Point(551, 183);
            this.allBackupDisksGroupBox.Name = "allBackupDisksGroupBox";
            this.allBackupDisksGroupBox.Size = new System.Drawing.Size(217, 108);
            this.allBackupDisksGroupBox.TabIndex = 67;
            this.allBackupDisksGroupBox.TabStop = false;
            this.allBackupDisksGroupBox.Text = "All Backup disks";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkConnectedBackupDiskButton);
            this.groupBox1.Controls.Add(this.checkDiskAndDeleteButton);
            this.groupBox1.Controls.Add(this.checkBackupDeleteAndCopyButton);
            this.groupBox1.Controls.Add(this.copyFilesToBackupDiskButton);
            this.groupBox1.Location = new System.Drawing.Point(551, 9);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(217, 134);
            this.groupBox1.TabIndex = 68;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Connected Backup disk";
            // 
            // listFilesGroupBox
            // 
            this.listFilesGroupBox.Controls.Add(this.listFilesNotOnBackupDiskButton);
            this.listFilesGroupBox.Controls.Add(this.listFilesNotCheckedInXXButton);
            this.listFilesGroupBox.Controls.Add(this.listMoviesWithMultipleFilesButton);
            this.listFilesGroupBox.Controls.Add(this.listFilesWithDuplicateContentHashcodesButton);
            this.listFilesGroupBox.Location = new System.Drawing.Point(290, 9);
            this.listFilesGroupBox.Name = "listFilesGroupBox";
            this.listFilesGroupBox.Size = new System.Drawing.Size(217, 138);
            this.listFilesGroupBox.TabIndex = 69;
            this.listFilesGroupBox.TabStop = false;
            this.listFilesGroupBox.Text = "List files";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.updateMasterFilesButton);
            this.groupBox4.Controls.Add(this.speedTestButton);
            this.groupBox4.Controls.Add(this.recalculateAllHashesButton);
            this.groupBox4.Location = new System.Drawing.Point(29, 130);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(217, 110);
            this.groupBox4.TabIndex = 70;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "All Master folders";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(30, 41);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(102, 13);
            this.label6.TabIndex = 71;
            this.label6.Text = "Current backup disk";
            // 
            // currentBackupDiskTextBox
            // 
            this.currentBackupDiskTextBox.Location = new System.Drawing.Point(140, 39);
            this.currentBackupDiskTextBox.Name = "currentBackupDiskTextBox";
            this.currentBackupDiskTextBox.ReadOnly = true;
            this.currentBackupDiskTextBox.Size = new System.Drawing.Size(106, 20);
            this.currentBackupDiskTextBox.TabIndex = 72;
            // 
            // backupDiskCapacityTextBox
            // 
            this.backupDiskCapacityTextBox.Location = new System.Drawing.Point(140, 67);
            this.backupDiskCapacityTextBox.Name = "backupDiskCapacityTextBox";
            this.backupDiskCapacityTextBox.ReadOnly = true;
            this.backupDiskCapacityTextBox.Size = new System.Drawing.Size(106, 20);
            this.backupDiskCapacityTextBox.TabIndex = 74;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(84, 69);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(48, 13);
            this.label7.TabIndex = 73;
            this.label7.Text = "Capacity";
            // 
            // backupDiskAvailableTextBox
            // 
            this.backupDiskAvailableTextBox.Location = new System.Drawing.Point(140, 95);
            this.backupDiskAvailableTextBox.Name = "backupDiskAvailableTextBox";
            this.backupDiskAvailableTextBox.ReadOnly = true;
            this.backupDiskAvailableTextBox.Size = new System.Drawing.Size(106, 20);
            this.backupDiskAvailableTextBox.TabIndex = 76;
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(82, 97);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(50, 13);
            this.label8.TabIndex = 75;
            this.label8.Text = "Available";
            // 
            // notOnABackupDiskTextBox
            // 
            this.notOnABackupDiskTextBox.Location = new System.Drawing.Point(111, 285);
            this.notOnABackupDiskTextBox.Name = "notOnABackupDiskTextBox";
            this.notOnABackupDiskTextBox.ReadOnly = true;
            this.notOnABackupDiskTextBox.Size = new System.Drawing.Size(67, 20);
            this.notOnABackupDiskTextBox.TabIndex = 80;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(2, 287);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(109, 13);
            this.label9.TabIndex = 79;
            this.label9.Text = "Not on a backup disk";
            // 
            // totalFilesTextBox
            // 
            this.totalFilesTextBox.Location = new System.Drawing.Point(111, 257);
            this.totalFilesTextBox.Name = "totalFilesTextBox";
            this.totalFilesTextBox.ReadOnly = true;
            this.totalFilesTextBox.Size = new System.Drawing.Size(67, 20);
            this.totalFilesTextBox.TabIndex = 78;
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(59, 259);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(52, 13);
            this.label10.TabIndex = 77;
            this.label10.Text = "Total files";
            // 
            // notOnABackupDiskSizeTextBox1
            // 
            this.notOnABackupDiskSizeTextBox1.Location = new System.Drawing.Point(184, 285);
            this.notOnABackupDiskSizeTextBox1.Name = "notOnABackupDiskSizeTextBox1";
            this.notOnABackupDiskSizeTextBox1.ReadOnly = true;
            this.notOnABackupDiskSizeTextBox1.Size = new System.Drawing.Size(62, 20);
            this.notOnABackupDiskSizeTextBox1.TabIndex = 82;
            // 
            // totalFilesSizeTextBox
            // 
            this.totalFilesSizeTextBox.Location = new System.Drawing.Point(184, 257);
            this.totalFilesSizeTextBox.Name = "totalFilesSizeTextBox";
            this.totalFilesSizeTextBox.ReadOnly = true;
            this.totalFilesSizeTextBox.Size = new System.Drawing.Size(62, 20);
            this.totalFilesSizeTextBox.TabIndex = 81;
            // 
            // estimatedFinishTimeTextBox
            // 
            this.estimatedFinishTimeTextBox.Location = new System.Drawing.Point(417, 285);
            this.estimatedFinishTimeTextBox.Name = "estimatedFinishTimeTextBox";
            this.estimatedFinishTimeTextBox.ReadOnly = true;
            this.estimatedFinishTimeTextBox.Size = new System.Drawing.Size(67, 20);
            this.estimatedFinishTimeTextBox.TabIndex = 84;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(308, 287);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(102, 13);
            this.label11.TabIndex = 83;
            this.label11.Text = "Estimated finish time";
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1309, 336);
            this.Controls.Add(this.estimatedFinishTimeTextBox);
            this.Controls.Add(this.label11);
            this.Controls.Add(this.notOnABackupDiskSizeTextBox1);
            this.Controls.Add(this.totalFilesSizeTextBox);
            this.Controls.Add(this.notOnABackupDiskTextBox);
            this.Controls.Add(this.label9);
            this.Controls.Add(this.totalFilesTextBox);
            this.Controls.Add(this.label10);
            this.Controls.Add(this.backupDiskAvailableTextBox);
            this.Controls.Add(this.label8);
            this.Controls.Add(this.backupDiskCapacityTextBox);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.currentBackupDiskTextBox);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.listFilesGroupBox);
            this.Controls.Add(this.speedTestDisksButton);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.allBackupDisksGroupBox);
            this.Controls.Add(this.cancelButton);
            this.Controls.Add(this.statusStrip);
            this.Controls.Add(this.processesGroupBox);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.listFilesOnBackupDiskGroupBox);
            this.Controls.Add(this.listFilesInMasterFolderGroupBox);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.pushoverGroupBox);
            this.Controls.Add(this.backupDiskTextBox);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Main";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Backup Manager";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Main_FormClosed);
            this.pushoverGroupBox.ResumeLayout(false);
            this.pushoverGroupBox.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.listFilesInMasterFolderGroupBox.ResumeLayout(false);
            this.listFilesOnBackupDiskGroupBox.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.processesGroupBox.ResumeLayout(false);
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            this.allBackupDisksGroupBox.ResumeLayout(false);
            this.groupBox1.ResumeLayout(false);
            this.listFilesGroupBox.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button updateMasterFilesButton;
        private System.Windows.Forms.Button checkConnectedBackupDiskButton;
        private System.Windows.Forms.Button copyFilesToBackupDiskButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox backupDiskTextBox;
        private System.Windows.Forms.Button listFilesNotOnBackupDiskButton;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Button recalculateAllHashesButton;
        private System.Windows.Forms.Button checkDiskAndDeleteButton;
        private System.Windows.Forms.Button scheduledBackupTimerButton;
        private System.Windows.Forms.Button listFilesOnBackupDiskButton;
        private System.Windows.Forms.Button listFilesInMasterFolderButton;
        private System.Windows.Forms.ComboBox masterFoldersComboBox;
        private System.Windows.Forms.Button listFilesNotCheckedInXXButton;
        private System.Windows.Forms.Button restoreFilesToMasterFolderButton;
        private System.Windows.Forms.ComboBox restoreMasterFolderComboBox;
        private System.Windows.Forms.Button checkBackupDeleteAndCopyButton;
        private System.Windows.Forms.Button listMoviesWithMultipleFilesButton;
        private System.Windows.Forms.Button testPushoverHighButton;
        private System.Windows.Forms.Button testPushoverNormalButton;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button testPushoverEmergencyButton;
        private System.Windows.Forms.Button reportBackupDiskStatusButton;
        private System.Windows.Forms.Button speedTestButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox runOnTimerStartCheckBox;
        private System.Windows.Forms.Button monitoringButton;
        private System.Windows.Forms.Timer monitoringTimer;
        private System.Windows.Forms.Button killProcessesButton;
        private System.Windows.Forms.ComboBox listMasterFoldersComboBox;
        private System.Windows.Forms.GroupBox pushoverGroupBox;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox listFilesInMasterFolderGroupBox;
        private System.Windows.Forms.GroupBox listFilesOnBackupDiskGroupBox;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button testPushoverLowButton;
        private System.Windows.Forms.Button stopProcessButton;
        private System.Windows.Forms.ComboBox processesComboBox;
        private System.Windows.Forms.GroupBox processesGroupBox;
        private System.Windows.Forms.ComboBox listFilesComboBox;
        private System.Windows.Forms.Button listFilesWithDuplicateContentHashcodesButton;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel;
        private System.Windows.Forms.Button checkDeleteAndCopyAllBackupDisksButton;
        private System.Windows.Forms.Button cancelButton;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgressBar;
        private System.Windows.Forms.Button checkAllBackupDisksButton;
        private System.Windows.Forms.GroupBox allBackupDisksGroupBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button pushoverOnOffButton;
        private System.Windows.Forms.CheckBox pushoverEmergencyCheckBox;
        private System.Windows.Forms.CheckBox pushoverHighCheckBox;
        private System.Windows.Forms.CheckBox pushoverNormalCheckBox;
        private System.Windows.Forms.CheckBox pushoverLowCheckBox;
        private System.Windows.Forms.Button speedTestDisksButton;
        private System.Windows.Forms.DateTimePicker scheduledDateTimePicker;
        private System.Windows.Forms.GroupBox listFilesGroupBox;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox currentBackupDiskTextBox;
        private System.Windows.Forms.TextBox backupDiskCapacityTextBox;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox backupDiskAvailableTextBox;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.TextBox notOnABackupDiskTextBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox totalFilesTextBox;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox notOnABackupDiskSizeTextBox1;
        private System.Windows.Forms.TextBox totalFilesSizeTextBox;
        private System.Windows.Forms.TextBox estimatedFinishTimeTextBox;
        private System.Windows.Forms.Label label11;
    }
}

