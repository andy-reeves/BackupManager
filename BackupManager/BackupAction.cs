// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupAction.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager;

internal enum BackupAction
{
    General,

    ScanFolders,

    BackupFiles,

    CheckBackupDisk,

    Restore,

    Monitoring
}