// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupAction.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager;

internal enum BackupAction
{
    General,

    ScanDirectory,

    BackupFiles,

    CheckBackupDisk,

    Restore,

    Monitoring
}