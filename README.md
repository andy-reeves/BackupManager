# BackupManager

1. Update MediaBackup.xml with the nas folders you want to backup MasterFolders and IndexFolders and (optionally) your PushbulletApiKey.
2. Connect an external drive and format it
3. Name the drive 'backup 1'
4. Share the drive with full access. This is where all backup files will be copied to.
5. Create a folder on the drive called 'backup 1'
6. Run the BackupManager.exe - it will default to copying the files to a share called 'backup' on this machine.
7. Click option 1 - this will build a complete list of all the files that will be monitored and backup up. The output of the scan wil lbe in your Documents folder called 'backup_BuildMasterFileList.txt' 
8. Click option 3 - this will backup all the monitored files

Option 2 (and others) will process the files and produce a report in your MyDocuments folder.
When you're happy that all files are being monitored edit the MediaBackup.xml file and set the <ScheduledBackupRepeatInterval> to 1440. This will make the backup run every 24 hours.

Then set the <StartScheduledBackup> to true.
  
Then drop a shortcut to BackupManager.exe in the Windows startup folder.

Run the BackupManager.exe one more time.
  
