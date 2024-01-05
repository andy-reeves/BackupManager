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

    ProcessFiles,

    BackupFiles,

    CheckBackupDisk,

    Restore,

    Monitoring
}