// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="BackupAction.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace BackupManager;

internal enum BackupAction
{
    General,

    ScheduledBackup,

    ScanDirectory,

    ProcessFiles,

    CopyFiles,

    CheckBackupDisk,

    Restore,

    ApplicationMonitoring,

    SpeedTest,

    CheckingSymbolicLinks,

    Error
}