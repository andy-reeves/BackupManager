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
            this.updateMasterFilesButton = new System.Windows.Forms.Button();
            this.checkConnectedBackupDriveButton = new System.Windows.Forms.Button();
            this.copyFilesToBackupDriveButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.backupDiskTextBox = new System.Windows.Forms.TextBox();
            this.listFilesNotOnBackupButton = new System.Windows.Forms.Button();
            this.toolTip = new System.Windows.Forms.ToolTip(this.components);
            this.button1 = new System.Windows.Forms.Button();
            this.backupHashCodeCheckedButton = new System.Windows.Forms.Button();
            this.checkDiskAndDeleteButton = new System.Windows.Forms.Button();
            this.button3 = new System.Windows.Forms.Button();
            this.checkBackupDeleteAndCopyButton = new System.Windows.Forms.Button();
            this.listMoviesWithMultipleFilesButton = new System.Windows.Forms.Button();
            this.reportBackupDiskStatusButton = new System.Windows.Forms.Button();
            this.speedTestButton = new System.Windows.Forms.Button();
            this.monitoringButton = new System.Windows.Forms.Button();
            this.timerButton = new System.Windows.Forms.Button();
            this.listFilesOnBackupDiskButton = new System.Windows.Forms.Button();
            this.listFilesTextBox = new System.Windows.Forms.TextBox();
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
            ((System.ComponentModel.ISupportInitialize)(this.hoursNumericUpDown)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.minutesNumericUpDown)).BeginInit();
            this.SuspendLayout();
            // 
            // updateMasterFilesButton
            // 
            this.updateMasterFilesButton.Location = new System.Drawing.Point(10, 33);
            this.updateMasterFilesButton.Name = "updateMasterFilesButton";
            this.updateMasterFilesButton.Size = new System.Drawing.Size(217, 23);
            this.updateMasterFilesButton.TabIndex = 0;
            this.updateMasterFilesButton.Text = "1. Update file list from all master folders";
            this.toolTip.SetToolTip(this.updateMasterFilesButton, "Resets the entire collection of backup files. Extra entries are removed if no lon" +
        "ger there.");
            this.updateMasterFilesButton.UseVisualStyleBackColor = true;
            this.updateMasterFilesButton.Click += new System.EventHandler(this.UpdateMasterFilesButtonClick);
            // 
            // checkConnectedBackupDriveButton
            // 
            this.checkConnectedBackupDriveButton.Location = new System.Drawing.Point(10, 290);
            this.checkConnectedBackupDriveButton.Name = "checkConnectedBackupDriveButton";
            this.checkConnectedBackupDriveButton.Size = new System.Drawing.Size(217, 39);
            this.checkConnectedBackupDriveButton.TabIndex = 1;
            this.checkConnectedBackupDriveButton.Text = "Check connected backup disk";
            this.toolTip.SetToolTip(this.checkConnectedBackupDriveButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.checkConnectedBackupDriveButton.UseVisualStyleBackColor = true;
            this.checkConnectedBackupDriveButton.Click += new System.EventHandler(this.CheckConnectedBackupDriveButton_Click);
            // 
            // copyFilesToBackupDriveButton
            // 
            this.copyFilesToBackupDriveButton.Location = new System.Drawing.Point(10, 88);
            this.copyFilesToBackupDriveButton.Name = "copyFilesToBackupDriveButton";
            this.copyFilesToBackupDriveButton.Size = new System.Drawing.Size(217, 23);
            this.copyFilesToBackupDriveButton.TabIndex = 5;
            this.copyFilesToBackupDriveButton.Text = "3. Copy files from master to backup disk";
            this.toolTip.SetToolTip(this.copyFilesToBackupDriveButton, "Copies any files without a Backup disk set to a connected Backup drive.");
            this.copyFilesToBackupDriveButton.UseVisualStyleBackColor = true;
            this.copyFilesToBackupDriveButton.Click += new System.EventHandler(this.CopyFilesToBackupDriveButtonClick);
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
            // listFilesNotOnBackupButton
            // 
            this.listFilesNotOnBackupButton.Location = new System.Drawing.Point(10, 60);
            this.listFilesNotOnBackupButton.Name = "listFilesNotOnBackupButton";
            this.listFilesNotOnBackupButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesNotOnBackupButton.TabIndex = 11;
            this.listFilesNotOnBackupButton.Text = "2. Files not on a Backup disk";
            this.toolTip.SetToolTip(this.listFilesNotOnBackupButton, "Outputs files that are not yet on a Backup drive.");
            this.listFilesNotOnBackupButton.UseVisualStyleBackColor = true;
            this.listFilesNotOnBackupButton.Click += new System.EventHandler(this.ListFilesNotOnBackupDriveButton_Click);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(10, 183);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(217, 23);
            this.button1.TabIndex = 16;
            this.button1.Text = "Recalculate all Hashes from Master Files";
            this.toolTip.SetToolTip(this.button1, "Recalculates all the Hashcodes from the Master Files. Only use this is the hash a" +
        "lgorithm has been changed.");
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.RecalculateHashcodesButton_Click);
            // 
            // backupHashCodeCheckedButton
            // 
            this.backupHashCodeCheckedButton.Location = new System.Drawing.Point(10, 117);
            this.backupHashCodeCheckedButton.Name = "backupHashCodeCheckedButton";
            this.backupHashCodeCheckedButton.Size = new System.Drawing.Size(217, 23);
            this.backupHashCodeCheckedButton.TabIndex = 17;
            this.backupHashCodeCheckedButton.Text = "4. Files without BackupDiskChecked set";
            this.toolTip.SetToolTip(this.backupHashCodeCheckedButton, "Lists files that have a null BackupDiskChecked value. This means it\'s been copied" +
        " to a backup disk but the Hashcode is now different. We\'ve probably updated the " +
        "master file.");
            this.backupHashCodeCheckedButton.UseVisualStyleBackColor = true;
            this.backupHashCodeCheckedButton.Click += new System.EventHandler(this.BackupHashCodeCheckedButton_Click);
            // 
            // checkDiskAndDeleteButton
            // 
            this.checkDiskAndDeleteButton.Location = new System.Drawing.Point(10, 344);
            this.checkDiskAndDeleteButton.Name = "checkDiskAndDeleteButton";
            this.checkDiskAndDeleteButton.Size = new System.Drawing.Size(217, 39);
            this.checkDiskAndDeleteButton.TabIndex = 19;
            this.checkDiskAndDeleteButton.Text = "Check connected backup disk (and remove extra files)";
            this.toolTip.SetToolTip(this.checkDiskAndDeleteButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkDiskAndDeleteButton.UseVisualStyleBackColor = true;
            this.checkDiskAndDeleteButton.Click += new System.EventHandler(this.checkDiskAndDeleteButton_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(10, 144);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(217, 35);
            this.button3.TabIndex = 20;
            this.button3.Text = "5. Clear backupdisk for files without BackupDiskChecked";
            this.toolTip.SetToolTip(this.button3, "Lists files that have a null BackupDiskChecked value. This means it\'s been copied" +
        " to a backup disk but the Hashcode is now different. We\'ve probably updated the " +
        "master file.");
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.FilesWithoutBackupDiskChecked_Click);
            // 
            // checkBackupDeleteAndCopyButton
            // 
            this.checkBackupDeleteAndCopyButton.Location = new System.Drawing.Point(244, 344);
            this.checkBackupDeleteAndCopyButton.Name = "checkBackupDeleteAndCopyButton";
            this.checkBackupDeleteAndCopyButton.Size = new System.Drawing.Size(217, 39);
            this.checkBackupDeleteAndCopyButton.TabIndex = 36;
            this.checkBackupDeleteAndCopyButton.Text = "Check backup disk, delete files and then copy files";
            this.toolTip.SetToolTip(this.checkBackupDeleteAndCopyButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkBackupDeleteAndCopyButton.UseVisualStyleBackColor = true;
            this.checkBackupDeleteAndCopyButton.Click += new System.EventHandler(this.checkBackupDeleteAndCopyButton_Click);
            // 
            // listMoviesWithMultipleFilesButton
            // 
            this.listMoviesWithMultipleFilesButton.Location = new System.Drawing.Point(244, 60);
            this.listMoviesWithMultipleFilesButton.Name = "listMoviesWithMultipleFilesButton";
            this.listMoviesWithMultipleFilesButton.Size = new System.Drawing.Size(217, 23);
            this.listMoviesWithMultipleFilesButton.TabIndex = 37;
            this.listMoviesWithMultipleFilesButton.Text = "List movies with multiple movie files";
            this.toolTip.SetToolTip(this.listMoviesWithMultipleFilesButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.listMoviesWithMultipleFilesButton.UseVisualStyleBackColor = true;
            this.listMoviesWithMultipleFilesButton.Click += new System.EventHandler(this.listMoviesWithMultipleFilesButton_Click);
            // 
            // reportBackupDiskStatusButton
            // 
            this.reportBackupDiskStatusButton.Location = new System.Drawing.Point(244, 173);
            this.reportBackupDiskStatusButton.Name = "reportBackupDiskStatusButton";
            this.reportBackupDiskStatusButton.Size = new System.Drawing.Size(217, 23);
            this.reportBackupDiskStatusButton.TabIndex = 43;
            this.reportBackupDiskStatusButton.Text = "Report backup disk status";
            this.toolTip.SetToolTip(this.reportBackupDiskStatusButton, "Outputs files that are not yet on a Backup drive.");
            this.reportBackupDiskStatusButton.UseVisualStyleBackColor = true;
            this.reportBackupDiskStatusButton.Click += new System.EventHandler(this.reportBackupDiskStatusButton_Click);
            // 
            // speedTestButton
            // 
            this.speedTestButton.Location = new System.Drawing.Point(244, 204);
            this.speedTestButton.Name = "speedTestButton";
            this.speedTestButton.Size = new System.Drawing.Size(217, 23);
            this.speedTestButton.TabIndex = 44;
            this.speedTestButton.Text = "Speed test all master folders";
            this.toolTip.SetToolTip(this.speedTestButton, "Outputs files that are not yet on a Backup drive.");
            this.speedTestButton.UseVisualStyleBackColor = true;
            this.speedTestButton.Click += new System.EventHandler(this.speedTestButton_Click);
            // 
            // monitoringButton
            // 
            this.monitoringButton.Location = new System.Drawing.Point(358, 315);
            this.monitoringButton.Name = "monitoringButton";
            this.monitoringButton.Size = new System.Drawing.Size(103, 23);
            this.monitoringButton.TabIndex = 50;
            this.monitoringButton.Text = "Start monitoring";
            this.toolTip.SetToolTip(this.monitoringButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.monitoringButton.UseVisualStyleBackColor = true;
            this.monitoringButton.Click += new System.EventHandler(this.monitoringButton_Click);
            // 
            // timerButton
            // 
            this.timerButton.Location = new System.Drawing.Point(245, 315);
            this.timerButton.Name = "timerButton";
            this.timerButton.Size = new System.Drawing.Size(103, 23);
            this.timerButton.TabIndex = 21;
            this.timerButton.Text = "Start timer";
            this.timerButton.UseVisualStyleBackColor = true;
            this.timerButton.Click += new System.EventHandler(this.timerButton_Click);
            // 
            // listFilesOnBackupDiskButton
            // 
            this.listFilesOnBackupDiskButton.Location = new System.Drawing.Point(93, 210);
            this.listFilesOnBackupDiskButton.Name = "listFilesOnBackupDiskButton";
            this.listFilesOnBackupDiskButton.Size = new System.Drawing.Size(134, 23);
            this.listFilesOnBackupDiskButton.TabIndex = 24;
            this.listFilesOnBackupDiskButton.Text = "List files on backup disk";
            this.listFilesOnBackupDiskButton.UseVisualStyleBackColor = true;
            this.listFilesOnBackupDiskButton.Click += new System.EventHandler(this.listFilesOnBackupDiskButton_Click);
            // 
            // listFilesTextBox
            // 
            this.listFilesTextBox.Location = new System.Drawing.Point(10, 212);
            this.listFilesTextBox.Name = "listFilesTextBox";
            this.listFilesTextBox.Size = new System.Drawing.Size(77, 20);
            this.listFilesTextBox.TabIndex = 25;
            this.listFilesTextBox.Text = "backup 1";
            // 
            // listFilesInMasterFolderButton
            // 
            this.listFilesInMasterFolderButton.Location = new System.Drawing.Point(244, 235);
            this.listFilesInMasterFolderButton.Name = "listFilesInMasterFolderButton";
            this.listFilesInMasterFolderButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesInMasterFolderButton.TabIndex = 26;
            this.listFilesInMasterFolderButton.Text = "List files on MasterFolder";
            this.listFilesInMasterFolderButton.UseVisualStyleBackColor = true;
            this.listFilesInMasterFolderButton.Click += new System.EventHandler(this.listFilesInMasterFolderButton_Click);
            // 
            // masterFoldersComboBox
            // 
            this.masterFoldersComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.masterFoldersComboBox.FormattingEnabled = true;
            this.masterFoldersComboBox.Location = new System.Drawing.Point(99, 237);
            this.masterFoldersComboBox.Name = "masterFoldersComboBox";
            this.masterFoldersComboBox.Size = new System.Drawing.Size(128, 21);
            this.masterFoldersComboBox.TabIndex = 29;
            // 
            // listFilesNotCheckedInXXButton
            // 
            this.listFilesNotCheckedInXXButton.Location = new System.Drawing.Point(244, 33);
            this.listFilesNotCheckedInXXButton.Name = "listFilesNotCheckedInXXButton";
            this.listFilesNotCheckedInXXButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesNotCheckedInXXButton.TabIndex = 30;
            this.listFilesNotCheckedInXXButton.Text = "Check for old Backup disks";
            this.listFilesNotCheckedInXXButton.UseVisualStyleBackColor = true;
            this.listFilesNotCheckedInXXButton.Click += new System.EventHandler(this.CheckForOldBackupDisks_Click);
            // 
            // restoreFilesToMasterFolderButton
            // 
            this.restoreFilesToMasterFolderButton.Location = new System.Drawing.Point(244, 261);
            this.restoreFilesToMasterFolderButton.Name = "restoreFilesToMasterFolderButton";
            this.restoreFilesToMasterFolderButton.Size = new System.Drawing.Size(217, 23);
            this.restoreFilesToMasterFolderButton.TabIndex = 31;
            this.restoreFilesToMasterFolderButton.Text = "Restore files to master folder from backup";
            this.restoreFilesToMasterFolderButton.UseVisualStyleBackColor = true;
            this.restoreFilesToMasterFolderButton.Click += new System.EventHandler(this.restoreFilesButton_Click);
            // 
            // restoreMasterFolderComboBox
            // 
            this.restoreMasterFolderComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.restoreMasterFolderComboBox.FormattingEnabled = true;
            this.restoreMasterFolderComboBox.Location = new System.Drawing.Point(99, 263);
            this.restoreMasterFolderComboBox.Name = "restoreMasterFolderComboBox";
            this.restoreMasterFolderComboBox.Size = new System.Drawing.Size(128, 21);
            this.restoreMasterFolderComboBox.TabIndex = 32;
            // 
            // testPushoverHighButton
            // 
            this.testPushoverHighButton.Location = new System.Drawing.Point(244, 117);
            this.testPushoverHighButton.Name = "testPushoverHighButton";
            this.testPushoverHighButton.Size = new System.Drawing.Size(217, 23);
            this.testPushoverHighButton.TabIndex = 38;
            this.testPushoverHighButton.Text = "pushover High";
            this.testPushoverHighButton.UseVisualStyleBackColor = true;
            this.testPushoverHighButton.Click += new System.EventHandler(this.testPushoverHighButton_Click);
            // 
            // testPushoverNormalButton
            // 
            this.testPushoverNormalButton.Location = new System.Drawing.Point(244, 88);
            this.testPushoverNormalButton.Name = "testPushoverNormalButton";
            this.testPushoverNormalButton.Size = new System.Drawing.Size(217, 23);
            this.testPushoverNormalButton.TabIndex = 39;
            this.testPushoverNormalButton.Text = "pushover Normal";
            this.testPushoverNormalButton.UseVisualStyleBackColor = true;
            this.testPushoverNormalButton.Click += new System.EventHandler(this.testPushoverNormalButton_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(7, 240);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(86, 13);
            this.label3.TabIndex = 40;
            this.label3.Text = "Src master folder";
            this.label3.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(7, 266);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(92, 13);
            this.label4.TabIndex = 41;
            this.label4.Text = "Dest master folder";
            this.label4.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // testPushoverEmergencyButton
            // 
            this.testPushoverEmergencyButton.Location = new System.Drawing.Point(244, 144);
            this.testPushoverEmergencyButton.Name = "testPushoverEmergencyButton";
            this.testPushoverEmergencyButton.Size = new System.Drawing.Size(217, 23);
            this.testPushoverEmergencyButton.TabIndex = 42;
            this.testPushoverEmergencyButton.Text = "pushover Emergency";
            this.testPushoverEmergencyButton.UseVisualStyleBackColor = true;
            this.testPushoverEmergencyButton.Click += new System.EventHandler(this.testPushoverEmergencyButton_Click);
            // 
            // hoursNumericUpDown
            // 
            this.hoursNumericUpDown.Location = new System.Drawing.Point(272, 290);
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
            this.hoursNumericUpDown.ValueChanged += new System.EventHandler(this.hoursNumericUpDown_ValueChanged);
            // 
            // minutesNumericUpDown
            // 
            this.minutesNumericUpDown.Location = new System.Drawing.Point(316, 290);
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
            this.minutesNumericUpDown.ValueChanged += new System.EventHandler(this.minutesNumericUpDown_ValueChanged);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(242, 292);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(30, 13);
            this.label2.TabIndex = 47;
            this.label2.Text = "Time";
            this.label2.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(307, 293);
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
            this.runOnTimerStartCheckBox.Location = new System.Drawing.Point(358, 291);
            this.runOnTimerStartCheckBox.Name = "runOnTimerStartCheckBox";
            this.runOnTimerStartCheckBox.Size = new System.Drawing.Size(109, 17);
            this.runOnTimerStartCheckBox.TabIndex = 49;
            this.runOnTimerStartCheckBox.Text = "Run on timer start";
            this.runOnTimerStartCheckBox.UseVisualStyleBackColor = true;
            // 
            // monitoringTimer
            // 
            this.monitoringTimer.Interval = 60000;
            this.monitoringTimer.Tick += new System.EventHandler(this.monitoringTimer_Tick);
            // 
            // killProcessesButton
            // 
            this.killProcessesButton.Location = new System.Drawing.Point(244, 389);
            this.killProcessesButton.Name = "killProcessesButton";
            this.killProcessesButton.Size = new System.Drawing.Size(217, 23);
            this.killProcessesButton.TabIndex = 51;
            this.killProcessesButton.Text = "Kill processes / stop services";
            this.killProcessesButton.UseVisualStyleBackColor = true;
            this.killProcessesButton.Click += new System.EventHandler(this.killProcessesButton_Click);
            // 
            // Main
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(468, 447);
            this.Controls.Add(this.killProcessesButton);
            this.Controls.Add(this.monitoringButton);
            this.Controls.Add(this.runOnTimerStartCheckBox);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.minutesNumericUpDown);
            this.Controls.Add(this.hoursNumericUpDown);
            this.Controls.Add(this.speedTestButton);
            this.Controls.Add(this.reportBackupDiskStatusButton);
            this.Controls.Add(this.testPushoverEmergencyButton);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.testPushoverNormalButton);
            this.Controls.Add(this.testPushoverHighButton);
            this.Controls.Add(this.listMoviesWithMultipleFilesButton);
            this.Controls.Add(this.checkBackupDeleteAndCopyButton);
            this.Controls.Add(this.restoreMasterFolderComboBox);
            this.Controls.Add(this.restoreFilesToMasterFolderButton);
            this.Controls.Add(this.listFilesNotCheckedInXXButton);
            this.Controls.Add(this.masterFoldersComboBox);
            this.Controls.Add(this.listFilesInMasterFolderButton);
            this.Controls.Add(this.listFilesTextBox);
            this.Controls.Add(this.listFilesOnBackupDiskButton);
            this.Controls.Add(this.timerButton);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.checkDiskAndDeleteButton);
            this.Controls.Add(this.backupHashCodeCheckedButton);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.listFilesNotOnBackupButton);
            this.Controls.Add(this.backupDiskTextBox);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.copyFilesToBackupDriveButton);
            this.Controls.Add(this.checkConnectedBackupDriveButton);
            this.Controls.Add(this.updateMasterFilesButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "Main";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "Backup Manager";
            ((System.ComponentModel.ISupportInitialize)(this.hoursNumericUpDown)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.minutesNumericUpDown)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button updateMasterFilesButton;
        private System.Windows.Forms.Button checkConnectedBackupDriveButton;
        private System.Windows.Forms.Button copyFilesToBackupDriveButton;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox backupDiskTextBox;
        private System.Windows.Forms.Button listFilesNotOnBackupButton;
        private System.Windows.Forms.ToolTip toolTip;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button backupHashCodeCheckedButton;
        private System.Windows.Forms.Button checkDiskAndDeleteButton;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button timerButton;
        private System.Windows.Forms.Button listFilesOnBackupDiskButton;
        private System.Windows.Forms.TextBox listFilesTextBox;
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
    }
}

