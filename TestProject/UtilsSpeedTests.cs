// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsSpeedTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class UtilsSpeedTests
{
    static UtilsSpeedTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void SpeedTests()
    {
        // set up the cancellation token
        // start the AsyncSpeedTest (which should take 30 seconds or so)
        // wait 5 seconds
        // cancel the token

        // check the test files are removed
        var tokenSource = new CancellationTokenSource();
        var ct = tokenSource.Token;

        _ = Task.Run(() => Utils.DiskSpeedTest(@"c:\speedtest", 1000000000, 1, out _, out _, ct), ct).ContinueWith(static _ => { }, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.FromCurrentSynchronizationContext());
        Utils.Wait(100);
        tokenSource.Cancel();
        Utils.Wait(200);
        Assert.False(File.Exists(@"c:\speedtest\1test.tmp"));
    }
}