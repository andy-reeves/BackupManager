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
            this.timerButton = new System.Windows.Forms.Button();
            this.backupTimer = new System.Windows.Forms.Timer(this.components);
            this.timerTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.button2 = new System.Windows.Forms.Button();
            this.listFilesTextBox = new System.Windows.Forms.TextBox();
            this.button5 = new System.Windows.Forms.Button();
            this.masterFoldersComboBox = new System.Windows.Forms.ComboBox();
            this.button6 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // updateMasterFilesButton
            // 
            this.updateMasterFilesButton.Location = new System.Drawing.Point(64, 53);
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
            this.checkConnectedBackupDriveButton.Location = new System.Drawing.Point(64, 319);
            this.checkConnectedBackupDriveButton.Name = "checkConnectedBackupDriveButton";
            this.checkConnectedBackupDriveButton.Size = new System.Drawing.Size(217, 23);
            this.checkConnectedBackupDriveButton.TabIndex = 1;
            this.checkConnectedBackupDriveButton.Text = "Check connected backup disk";
            this.toolTip.SetToolTip(this.checkConnectedBackupDriveButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). Doesn\'t delete any files.");
            this.checkConnectedBackupDriveButton.UseVisualStyleBackColor = true;
            this.checkConnectedBackupDriveButton.Click += new System.EventHandler(this.CheckConnectedBackupDriveButtonClick);
            // 
            // copyFilesToBackupDriveButton
            // 
            this.copyFilesToBackupDriveButton.Location = new System.Drawing.Point(64, 137);
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
            this.label1.Location = new System.Drawing.Point(27, 19);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 6;
            this.label1.Text = "Backup drive";
            // 
            // backupDiskTextBox
            // 
            this.backupDiskTextBox.Location = new System.Drawing.Point(124, 16);
            this.backupDiskTextBox.Name = "backupDiskTextBox";
            this.backupDiskTextBox.Size = new System.Drawing.Size(217, 20);
            this.backupDiskTextBox.TabIndex = 7;
            this.backupDiskTextBox.Text = "\\\\192.168.1.5\\backup";
            // 
            // listFilesNotOnBackupButton
            // 
            this.listFilesNotOnBackupButton.Location = new System.Drawing.Point(64, 92);
            this.listFilesNotOnBackupButton.Name = "listFilesNotOnBackupButton";
            this.listFilesNotOnBackupButton.Size = new System.Drawing.Size(217, 23);
            this.listFilesNotOnBackupButton.TabIndex = 11;
            this.listFilesNotOnBackupButton.Text = "2. Files not on a Backup disk";
            this.toolTip.SetToolTip(this.listFilesNotOnBackupButton, "Outputs files that are not yet on a Backup drive.");
            this.listFilesNotOnBackupButton.UseVisualStyleBackColor = true;
            this.listFilesNotOnBackupButton.Click += new System.EventHandler(this.ListFilesNotOnBackupDriveButtonClick);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(64, 277);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(217, 23);
            this.button1.TabIndex = 16;
            this.button1.Text = "Recalculate all Hashes from Master Files";
            this.toolTip.SetToolTip(this.button1, "Recalculates all the Hashcodes from the Master Files. Only use this is the hash a" +
        "lgorithm has been changed.");
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.RecalculateHashcodesButtonClick);
            // 
            // backupHashCodeCheckedButton
            // 
            this.backupHashCodeCheckedButton.Location = new System.Drawing.Point(64, 182);
            this.backupHashCodeCheckedButton.Name = "backupHashCodeCheckedButton";
            this.backupHashCodeCheckedButton.Size = new System.Drawing.Size(217, 23);
            this.backupHashCodeCheckedButton.TabIndex = 17;
            this.backupHashCodeCheckedButton.Text = "4. Files without BackupDiskChecked set";
            this.toolTip.SetToolTip(this.backupHashCodeCheckedButton, "Lists files that have a null BackupDiskChecked value. This means it\'s been copied" +
        " to a backup disk but the Hashcode is now different. We\'ve probably updated the " +
        "master file.");
            this.backupHashCodeCheckedButton.UseVisualStyleBackColor = true;
            this.backupHashCodeCheckedButton.Click += new System.EventHandler(this.BackupHashCodeCheckedButtonClick);
            // 
            // checkDiskAndDeleteButton
            // 
            this.checkDiskAndDeleteButton.Location = new System.Drawing.Point(64, 362);
            this.checkDiskAndDeleteButton.Name = "checkDiskAndDeleteButton";
            this.checkDiskAndDeleteButton.Size = new System.Drawing.Size(217, 42);
            this.checkDiskAndDeleteButton.TabIndex = 19;
            this.checkDiskAndDeleteButton.Text = "Check connected backup disk (and remove extra files)";
            this.toolTip.SetToolTip(this.checkDiskAndDeleteButton, "Checks a connected backup disk.\r\nSets the BackupDisk and the BackupDiskChecked (i" +
        "f the HashCode is correct). DELETES FILES.");
            this.checkDiskAndDeleteButton.UseVisualStyleBackColor = true;
            this.checkDiskAndDeleteButton.Click += new System.EventHandler(this.checkDiskAndDeleteButton_Click);
            // 
            // button3
            // 
            this.button3.Location = new System.Drawing.Point(64, 225);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(217, 35);
            this.button3.TabIndex = 20;
            this.button3.Text = "5. Clear backupdisk for files without BackupDiskChecked";
            this.toolTip.SetToolTip(this.button3, "Lists files that have a null BackupDiskChecked value. This means it\'s been copied" +
        " to a backup disk but the Hashcode is now different. We\'ve probably updated the " +
        "master file.");
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // timerButton
            // 
            this.timerButton.Location = new System.Drawing.Point(64, 422);
            this.timerButton.Name = "timerButton";
            this.timerButton.Size = new System.Drawing.Size(75, 23);
            this.timerButton.TabIndex = 21;
            this.timerButton.Text = "Start timer";
            this.timerButton.UseVisualStyleBackColor = true;
            this.timerButton.Click += new System.EventHandler(this.timerButton_Click);
            // 
            // backupTimer
            // 
            this.backupTimer.Interval = 5000;
            this.backupTimer.Tick += new System.EventHandler(this.backupTimer_Tick);
            // 
            // timerTextBox
            // 
            this.timerTextBox.Location = new System.Drawing.Point(145, 424);
            this.timerTextBox.Name = "timerTextBox";
            this.timerTextBox.Size = new System.Drawing.Size(28, 20);
            this.timerTextBox.TabIndex = 22;
            this.timerTextBox.Text = "5";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(179, 427);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(28, 13);
            this.label2.TabIndex = 23;
            this.label2.Text = "mins";
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(64, 461);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(143, 23);
            this.button2.TabIndex = 24;
            this.button2.Text = "List files on backup disk";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // listFilesTextBox
            // 
            this.listFilesTextBox.Location = new System.Drawing.Point(213, 463);
            this.listFilesTextBox.Name = "listFilesTextBox";
            this.listFilesTextBox.Size = new System.Drawing.Size(68, 20);
            this.listFilesTextBox.TabIndex = 25;
            this.listFilesTextBox.Text = "backup 1";
            // 
            // button5
            // 
            this.button5.Location = new System.Drawing.Point(30, 497);
            this.button5.Name = "button5";
            this.button5.Size = new System.Drawing.Size(143, 23);
            this.button5.TabIndex = 26;
            this.button5.Text = "List files on MasterFolder";
            this.button5.UseVisualStyleBackColor = true;
            this.button5.Click += new System.EventHandler(this.button5_Click);
            // 
            // masterFoldersComboBox
            // 
            this.masterFoldersComboBox.BackColor = System.Drawing.SystemColors.Window;
            this.masterFoldersComboBox.FormattingEnabled = true;
            this.masterFoldersComboBox.Location = new System.Drawing.Point(179, 499);
            this.masterFoldersComboBox.Name = "masterFoldersComboBox";
            this.masterFoldersComboBox.Size = new System.Drawing.Size(167, 21);
            this.masterFoldersComboBox.TabIndex = 29;
            // 
            // button6
            // 
            this.button6.Location = new System.Drawing.Point(30, 532);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(277, 23);
            this.button6.TabIndex = 30;
            this.button6.Text = "List files with Backup hash not checked for 90 days";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.button6_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(350, 567);
            this.Controls.Add(this.button6);
            this.Controls.Add(this.masterFoldersComboBox);
            this.Controls.Add(this.button5);
            this.Controls.Add(this.listFilesTextBox);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.timerTextBox);
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
            this.Name = "Form1";
            this.Text = "Backup Manager";
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
        private System.Windows.Forms.Timer backupTimer;
        private System.Windows.Forms.TextBox timerTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.TextBox listFilesTextBox;
        private System.Windows.Forms.Button button5;
        private System.Windows.Forms.ComboBox masterFoldersComboBox;
        private System.Windows.Forms.Button button6;
    }
}

