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
            this.timerButton = new System.Windows.Forms.Button();
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
            this.hoursNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.minutesNumericUpDown = new System.Windows.Forms.NumericUpDown();
            this.label2 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.runOnTimerStartCheckBox = new System.Windows.Forms.CheckBox();
            this.monitoringTimer = new System.Windows.Forms.Timer(this.components);
            this.killProcessesButton = new System.Windows.Forms.Button();
            this.listMasterFoldersComboBox = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.testPushoverLowButton = new System.Windows.Forms.Button();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.listFilesComboBox = new System.Windows.Forms.ComboBox();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.stopProcessButton = new System.Windows.Forms.Button();
            this.processesComboBox = new System.Windows.Forms.ComboBox();
            this.groupBox6 = new System.Windows.Forms.GroupBox();
            ((System.ComponentModel.ISupportInitialize)(this.hoursNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.minutesNumericUpDown)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.groupBox6.SuspendLayout();
            this.SuspendLayout();
            // 
            // updateMasterFilesButton
            // 
            this.updateMasterFilesButton.Location = new System.Drawing.Point(10, 33);
            this.updateMasterFilesButton.Name = "updateMasterFilesButton";
            this.updateMasterFilesButton.Size = new System.Drawing.Size(217, 23);
            this.updateMasterFilesButton.TabIndex = 0;
            this.updateMasterFilesButton.Text = "Update file list from all master folders";
            this.toolTip.SetToolTip(this.updateMasterFilesButton, "Resets the entire collection of backup files. Extra entries are removed if no lon" +
        "ger there.");
            this.updateMasterFilesButton.UseVisualStyleBackColor = true;
            this.updateMasterFilesButton.Click += new System.EventHandler(this.UpdateMasterFilesButtonClick);
            // 
            // checkConnectedBackupDiskButton
            // 
            this.checkConnectedBackupDiskButton.Location = new System.Drawing.Point(10, 387);
            this.checkConnectedBackupDiskButton.Name = "checkConnectedBackupDiskButton";
            this.checkConnectedBackupDiskButton.Size = new System.Drawing.Size(217, 39);
            this.checkConnectedBackupDiskButton.TabIndex = 1;
            this.checkConnectedBackupDiskButton.Text = "Check connected backup disk";
            this.toolTip.SetToolTip(this.checkConnectedBackupDiskButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.checkConnectedBackupDiskButton.UseVisualStyleBackColor = true;
            this.checkConnectedBackupDiskButton.Click += new System.EventHandler(this.CheckConnectedBackupDiskButton_Click);
            // 
            // copyFilesToBackupDiskButton
            // 
            this.copyFilesToBackupDiskButton.Location = new System.Drawing.Point(10, 88);
            this.copyFilesToBackupDiskButton.Name = "copyFilesToBackupDiskButton";
            this.copyFilesToBackupDiskButton.Size = new System.Drawing.Size(217, 23);
            this.copyFilesToBackupDiskButton.TabIndex = 5;
            this.copyFilesToBackupDiskButton.Text = "Copy files from master to backup disk";
            this.toolTip.SetToolTip(this.copyFilesToBackupDiskButton, "Copies any files without a Backup disk set to a connected Backup drive.");
            this.copyFilesToBackupDiskButton.UseVisualStyleBackColor = true;
            this.copyFilesToBackupDiskButton.Click += new System.EventHandler(this.CopyFilesToBackupDiskButton_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(7, 12);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Backup drive";
            // 
            // backupDiskTextBox
            // 
            this.backupDiskTextBox.Location = new System.Drawing.Point(99, 9);
            this.backupDiskTextBox.Name = "backupDiskTextBox";
            this.backupDiskTextBox.Size = new System.Drawing.Size(128, 20);
            this.backupDiskTextBox.TabIndex = 7;
            this.backupDiskTextBox.Text = "d:\\";
            // 
            // listFilesNotOnBackupDiskButton
            // 
            this.listFilesNotOnBackupDiskButton.Location = new System.Drawing.Point(10, 60);
            this.listFilesNotOnBackupDiskButton.Name = "listFilesNotOnBackupDiskButton";
            this.listFilesNotOnBackupDiskButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesNotOnBackupDiskButton.TabIndex = 11;
            this.listFilesNotOnBackupDiskButton.Text = "List files not on a Backup disk";
            this.toolTip.SetToolTip(this.listFilesNotOnBackupDiskButton, "Outputs files that are not yet on a Backup drive.");
            this.listFilesNotOnBackupDiskButton.UseVisualStyleBackColor = true;
            this.listFilesNotOnBackupDiskButton.Click += new System.EventHandler(this.ListFilesNotOnBackupDiskButton_Click);
            // 
            // recalculateAllHashesButton
            // 
            this.recalculateAllHashesButton.Location = new System.Drawing.Point(10, 141);
            this.recalculateAllHashesButton.Name = "recalculateAllHashesButton";
            this.recalculateAllHashesButton.Size = new System.Drawing.Size(217, 23);
            this.recalculateAllHashesButton.TabIndex = 16;
            this.recalculateAllHashesButton.Text = "Recalculate all Hashes from Master Files";
            this.toolTip.SetToolTip(this.recalculateAllHashesButton, "Recalculates all the Hashcodes from the Master Files. Only use this is the hash a" +
        "lgorithm has been changed.");
            this.recalculateAllHashesButton.UseVisualStyleBackColor = true;
            this.recalculateAllHashesButton.Click += new System.EventHandler(this.RecalculateAllHashesButton_Click);
            // 
            // checkDiskAndDeleteButton
            // 
            this.checkDiskAndDeleteButton.Location = new System.Drawing.Point(10, 431);
            this.checkDiskAndDeleteButton.Name = "checkDiskAndDeleteButton";
            this.checkDiskAndDeleteButton.Size = new System.Drawing.Size(217, 39);
            this.checkDiskAndDeleteButton.TabIndex = 19;
            this.checkDiskAndDeleteButton.Text = "Check connected backup disk (and remove extra files)";
            this.toolTip.SetToolTip(this.checkDiskAndDeleteButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkDiskAndDeleteButton.UseVisualStyleBackColor = true;
            this.checkDiskAndDeleteButton.Click += new System.EventHandler(this.CheckDiskAndDeleteButton_Click);
            // 
            // checkBackupDeleteAndCopyButton
            // 
            this.checkBackupDeleteAndCopyButton.Location = new System.Drawing.Point(10, 475);
            this.checkBackupDeleteAndCopyButton.Name = "checkBackupDeleteAndCopyButton";
            this.checkBackupDeleteAndCopyButton.Size = new System.Drawing.Size(217, 39);
            this.checkBackupDeleteAndCopyButton.TabIndex = 36;
            this.checkBackupDeleteAndCopyButton.Text = "Check backup disk, delete files and then copy files";
            this.toolTip.SetToolTip(this.checkBackupDeleteAndCopyButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkBackupDeleteAndCopyButton.UseVisualStyleBackColor = true;
            this.checkBackupDeleteAndCopyButton.Click += new System.EventHandler(this.CheckBackupDeleteAndCopyButton_Click);
            // 
            // listMoviesWithMultipleFilesButton
            // 
            this.listMoviesWithMultipleFilesButton.Location = new System.Drawing.Point(10, 198);
            this.listMoviesWithMultipleFilesButton.Name = "listMoviesWithMultipleFilesButton";
            this.listMoviesWithMultipleFilesButton.Size = new System.Drawing.Size(217, 23);
            this.listMoviesWithMultipleFilesButton.TabIndex = 37;
            this.listMoviesWithMultipleFilesButton.Text = "List movies with multiple movie files";
            this.toolTip.SetToolTip(this.listMoviesWithMultipleFilesButton, "Lists any movies that have multiple video files in the same folder");
            this.listMoviesWithMultipleFilesButton.UseVisualStyleBackColor = true;
            this.listMoviesWithMultipleFilesButton.Click += new System.EventHandler(this.ListMoviesWithMultipleFilesButton_Click);
            // 
            // reportBackupDiskStatusButton
            // 
            this.reportBackupDiskStatusButton.Location = new System.Drawing.Point(10, 254);
            this.reportBackupDiskStatusButton.Name = "reportBackupDiskStatusButton";
            this.reportBackupDiskStatusButton.Size = new System.Drawing.Size(217, 23);
            this.reportBackupDiskStatusButton.TabIndex = 43;
            this.reportBackupDiskStatusButton.Text = "Report backup disk status";
            this.toolTip.SetToolTip(this.reportBackupDiskStatusButton, "Reports the status of each backup disk");
            this.reportBackupDiskStatusButton.UseVisualStyleBackColor = true;
            this.reportBackupDiskStatusButton.Click += new System.EventHandler(this.ReportBackupDiskStatusButton_Click);
            // 
            // speedTestButton
            // 
            this.speedTestButton.Location = new System.Drawing.Point(10, 283);
            this.speedTestButton.Name = "speedTestButton";
            this.speedTestButton.Size = new System.Drawing.Size(217, 23);
            this.speedTestButton.TabIndex = 44;
            this.speedTestButton.Text = "Speed test all master folders";
            this.toolTip.SetToolTip(this.speedTestButton, "Runs the speed test on all master folders");
            this.speedTestButton.UseVisualStyleBackColor = true;
            this.speedTestButton.Click += new System.EventHandler(this.SpeedTestButton_Click);
            // 
            // monitoringButton
            // 
            this.monitoringButton.Location = new System.Drawing.Point(10, 332);
            this.monitoringButton.Name = "monitoringButton";
            this.monitoringButton.Size = new System.Drawing.Size(217, 23);
            this.monitoringButton.TabIndex = 50;
            this.monitoringButton.Text = "Start monitoring";
            this.toolTip.SetToolTip(this.monitoringButton, "Starts the service monitoring");
            this.monitoringButton.UseVisualStyleBackColor = true;
            this.monitoringButton.Click += new System.EventHandler(this.MonitoringButton_Click);
            // 
            // listFilesWithDuplicateContentHashcodesButton
            // 
            this.listFilesWithDuplicateContentHashcodesButton.Location = new System.Drawing.Point(10, 227);
            this.listFilesWithDuplicateContentHashcodesButton.Name = "listFilesWithDuplicateContentHashcodesButton";
            this.listFilesWithDuplicateContentHashcodesButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesWithDuplicateContentHashcodesButton.TabIndex = 61;
            this.listFilesWithDuplicateContentHashcodesButton.Text = "List files with duplicate content hashcodes";
            this.toolTip.SetToolTip(this.listFilesWithDuplicateContentHashcodesButton, "List files with duplicate content hashcodes");
            this.listFilesWithDuplicateContentHashcodesButton.UseVisualStyleBackColor = true;
            this.listFilesWithDuplicateContentHashcodesButton.Click += new System.EventHandler(this.ListFilesWithDuplicateContentHashcodesButton_Click);
            // 
            // timerButton
            // 
            this.timerButton.Location = new System.Drawing.Point(125, 55);
            this.timerButton.Name = "timerButton";
            this.timerButton.Size = new System.Drawing.Size(69, 23);
            this.timerButton.TabIndex = 21;
            this.timerButton.Text = "Start";
            this.timerButton.UseVisualStyleBackColor = true;
            this.timerButton.Click += new System.EventHandler(this.TimerButton_Click);
            // 
            // listFilesOnBackupDiskButton
            // 
            this.listFilesOnBackupDiskButton.Location = new System.Drawing.Point(125, 24);
            this.listFilesOnBackupDiskButton.Name = "listFilesOnBackupDiskButton";
            this.listFilesOnBackupDiskButton.Size = new System.Drawing.Size(69, 23);
            this.listFilesOnBackupDiskButton.TabIndex = 24;
            this.listFilesOnBackupDiskButton.Text = "List";
            this.listFilesOnBackupDiskButton.UseVisualStyleBackColor = true;
            this.listFilesOnBackupDiskButton.Click += new System.EventHandler(this.ListFilesOnBackupDiskButton_Click);
            // 
            // listFilesInMasterFolderButton
            // 
            this.listFilesInMasterFolderButton.Location = new System.Drawing.Point(125, 19);
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
            this.masterFoldersComboBox.Location = new System.Drawing.Point(89, 20);
            this.masterFoldersComboBox.Name = "masterFoldersComboBox";
            this.masterFoldersComboBox.Size = new System.Drawing.Size(105, 21);
            this.masterFoldersComboBox.TabIndex = 29;
            // 
            // listFilesNotCheckedInXXButton
            // 
            this.listFilesNotCheckedInXXButton.Location = new System.Drawing.Point(10, 169);
            this.listFilesNotCheckedInXXButton.Name = "listFilesNotCheckedInXXButton";
            this.listFilesNotCheckedInXXButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesNotCheckedInXXButton.TabIndex = 30;
            this.listFilesNotCheckedInXXButton.Text = "List files on old Backup disks";
            this.listFilesNotCheckedInXXButton.UseVisualStyleBackColor = true;
            this.listFilesNotCheckedInXXButton.Click += new System.EventHandler(this.CheckForOldBackupDisks_Click);
            // 
            // restoreFilesToMasterFolderButton
            // 
            this.restoreFilesToMasterFolderButton.Location = new System.Drawing.Point(125, 73);
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
            this.restoreMasterFolderComboBox.Location = new System.Drawing.Point(89, 46);
            this.restoreMasterFolderComboBox.Name = "restoreMasterFolderComboBox";
            this.restoreMasterFolderComboBox.Size = new System.Drawing.Size(105, 21);
            this.restoreMasterFolderComboBox.TabIndex = 32;
            // 
            // testPushoverHighButton
            // 
            this.testPushoverHighButton.Location = new System.Drawing.Point(32, 47);
            this.testPushoverHighButton.Name = "testPushoverHighButton";
            this.testPushoverHighButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverHighButton.TabIndex = 38;
            this.testPushoverHighButton.Text = "High";
            this.testPushoverHighButton.UseVisualStyleBackColor = true;
            this.testPushoverHighButton.Click += new System.EventHandler(this.TestPushoverHighButton_Click);
            // 
            // testPushoverNormalButton
            // 
            this.testPushoverNormalButton.Location = new System.Drawing.Point(107, 18);
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
            this.testPushoverEmergencyButton.Location = new System.Drawing.Point(107, 47);
            this.testPushoverEmergencyButton.Name = "testPushoverEmergencyButton";
            this.testPushoverEmergencyButton.Size = new System.Drawing.Size(69, 23);
            this.testPushoverEmergencyButton.TabIndex = 42;
            this.testPushoverEmergencyButton.Text = "Emergency";
            this.testPushoverEmergencyButton.UseVisualStyleBackColor = true;
            this.testPushoverEmergencyButton.Click += new System.EventHandler(this.TestPushoverEmergencyButton_Click);
            // 
            // hoursNumericUpDown
            // 
            this.hoursNumericUpDown.Location = new System.Drawing.Point(32, 29);
            this.hoursNumericUpDown.Maximum = new decimal(new int[] {
            24,
            0,
            0,
            0});
            this.hoursNumericUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.hoursNumericUpDown.Name = "hoursNumericUpDown";
            this.hoursNumericUpDown.Size = new System.Drawing.Size(36, 20);
            this.hoursNumericUpDown.TabIndex = 45;
            this.hoursNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.hoursNumericUpDown.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.hoursNumericUpDown.ValueChanged += new System.EventHandler(this.HoursNumericUpDown_ValueChanged);
            // 
            // minutesNumericUpDown
            // 
            this.minutesNumericUpDown.Location = new System.Drawing.Point(76, 29);
            this.minutesNumericUpDown.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.minutesNumericUpDown.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            -2147483648});
            this.minutesNumericUpDown.Name = "minutesNumericUpDown";
            this.minutesNumericUpDown.Size = new System.Drawing.Size(36, 20);
            this.minutesNumericUpDown.TabIndex = 46;
            this.minutesNumericUpDown.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            this.minutesNumericUpDown.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.minutesNumericUpDown.ValueChanged += new System.EventHandler(this.MinutesNumericUpDown_ValueChanged);
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
            this.killProcessesButton.Size = new System.Drawing.Size(188, 23);
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
            this.listMasterFoldersComboBox.Size = new System.Drawing.Size(108, 21);
            this.listMasterFoldersComboBox.TabIndex = 52;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.testPushoverLowButton);
            this.groupBox1.Controls.Add(this.testPushoverNormalButton);
            this.groupBox1.Controls.Add(this.testPushoverHighButton);
            this.groupBox1.Controls.Add(this.testPushoverEmergencyButton);
            this.groupBox1.Location = new System.Drawing.Point(252, 337);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(207, 78);
            this.groupBox1.TabIndex = 54;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Pushover tests";
            // 
            // testPushoverLowButton
            // 
            this.testPushoverLowButton.Location = new System.Drawing.Point(32, 18);
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
            this.groupBox2.Location = new System.Drawing.Point(252, 125);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(207, 109);
            this.groupBox2.TabIndex = 55;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Restore from Backup";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.listFilesInMasterFolderButton);
            this.groupBox3.Controls.Add(this.listMasterFoldersComboBox);
            this.groupBox3.Location = new System.Drawing.Point(252, 15);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(207, 49);
            this.groupBox3.TabIndex = 56;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "List files in master folder";
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.listFilesComboBox);
            this.groupBox4.Controls.Add(this.listFilesOnBackupDiskButton);
            this.groupBox4.Location = new System.Drawing.Point(252, 68);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(207, 54);
            this.groupBox4.TabIndex = 57;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "List files on Backup disk";
            // 
            // listFilesComboBox
            // 
            this.listFilesComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.listFilesComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.listFilesComboBox.FormattingEnabled = true;
            this.listFilesComboBox.Location = new System.Drawing.Point(6, 24);
            this.listFilesComboBox.Name = "listFilesComboBox";
            this.listFilesComboBox.Size = new System.Drawing.Size(108, 21);
            this.listFilesComboBox.TabIndex = 54;
            // 
            // groupBox5
            // 
            this.groupBox5.Controls.Add(this.timerButton);
            this.groupBox5.Controls.Add(this.hoursNumericUpDown);
            this.groupBox5.Controls.Add(this.minutesNumericUpDown);
            this.groupBox5.Controls.Add(this.label2);
            this.groupBox5.Controls.Add(this.label5);
            this.groupBox5.Controls.Add(this.runOnTimerStartCheckBox);
            this.groupBox5.Location = new System.Drawing.Point(252, 245);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(207, 88);
            this.groupBox5.TabIndex = 58;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Scheduled Backup";
            // 
            // stopProcessButton
            // 
            this.stopProcessButton.Location = new System.Drawing.Point(125, 25);
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
            // groupBox6
            // 
            this.groupBox6.Controls.Add(this.killProcessesButton);
            this.groupBox6.Controls.Add(this.stopProcessButton);
            this.groupBox6.Controls.Add(this.processesComboBox);
            this.groupBox6.Location = new System.Drawing.Point(252, 425);
            this.groupBox6.Name = "groupBox6";
            this.groupBox6.Size = new System.Drawing.Size(207, 90);
            this.groupBox6.TabIndex = 60;
            this.groupBox6.TabStop = false;
            this.groupBox6.Text = "Processes/Services";
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(469, 519);
            this.Controls.Add(this.listFilesWithDuplicateContentHashcodesButton);
            this.Controls.Add(this.groupBox6);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.monitoringButton);
            this.Controls.Add(this.speedTestButton);
            this.Controls.Add(this.reportBackupDiskStatusButton);
            this.Controls.Add(this.listMoviesWithMultipleFilesButton);
            this.Controls.Add(this.checkBackupDeleteAndCopyButton);
            this.Controls.Add(this.listFilesNotCheckedInXXButton);
            this.Controls.Add(this.checkDiskAndDeleteButton);
            this.Controls.Add(this.recalculateAllHashesButton);
            this.Controls.Add(this.listFilesNotOnBackupDiskButton);
            this.Controls.Add(this.backupDiskTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.copyFilesToBackupDiskButton);
            this.Controls.Add(this.checkConnectedBackupDiskButton);
            this.Controls.Add(this.updateMasterFilesButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "Main";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Backup Manager";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.Main_FormClosed);
            ((System.ComponentModel.ISupportInitialize)(this.hoursNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.minutesNumericUpDown)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.groupBox5.PerformLayout();
            this.groupBox6.ResumeLayout(false);
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
        private System.Windows.Forms.Button timerButton;
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
        private System.Windows.Forms.NumericUpDown hoursNumericUpDown;
        private System.Windows.Forms.NumericUpDown minutesNumericUpDown;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.CheckBox runOnTimerStartCheckBox;
        private System.Windows.Forms.Button monitoringButton;
        private System.Windows.Forms.Timer monitoringTimer;
        private System.Windows.Forms.Button killProcessesButton;
        private System.Windows.Forms.ComboBox listMasterFoldersComboBox;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button testPushoverLowButton;
        private System.Windows.Forms.Button stopProcessButton;
        private System.Windows.Forms.ComboBox processesComboBox;
        private System.Windows.Forms.GroupBox groupBox6;
        private System.Windows.Forms.ComboBox listFilesComboBox;
        private System.Windows.Forms.Button listFilesWithDuplicateContentHashcodesButton;
    }
}

