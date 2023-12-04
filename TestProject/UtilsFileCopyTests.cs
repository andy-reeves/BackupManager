// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsFileCopyTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
global using Xunit;

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsFileCopyTests
{
    static UtilsFileCopyTests()
    {
        var mediaBackup = MediaBackup.Load(Path.Combine(Utils.GetProjectPath(typeof(FileRulesUnitTest)), "..\\BackupManager\\MediaBackup.xml"));
        Utils.Config = mediaBackup.Config;
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void FileCopy()
    {
        for (var i = 0; i < 5; i++)
        {
            var path1 = Path.Combine(Path.GetTempPath(), "FileCopy");
            if (Directory.Exists(path1)) Directory.Delete(path1, true);
            var file1 = Path.Combine(path1, "test1.txt");
            var file2 = Path.Combine(path1, "test2.txt");
            Utils.EnsureDirectoriesForDirectoryPath(path1);
            Assert.False(File.Exists(file2));
            Utils.CreateFile(file1);
            Assert.Equal("098f6bcd4621d373cade4e832627b4f6", Utils.GetShortMd5HashFromFile(file1));
            Assert.True(Utils.FileCopy(file1, file2));
            Assert.True(File.Exists(file2));
            Assert.Equal("098f6bcd4621d373cade4e832627b4f6", Utils.GetShortMd5HashFromFile(file2));

            // Delete the folders we created
            if (Directory.Exists(path1)) Directory.Delete(path1, true);
        }
    }
}
#endif