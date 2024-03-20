// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsFileCopyTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

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
            if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
            var file1 = Path.Combine(path1, "test1.txt");
            var file2 = Path.Combine(path1, "test2.txt");
            Utils.Directory.EnsurePath(path1);
            Assert.False(File.Exists(file2));
            Utils.File.Create(file1);
            Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", Utils.File.GetShortMd5Hash(file1));
            var ct = new CancellationToken();
            Assert.True(Utils.File.Copy(file1, file2, ct));
            Assert.True(File.Exists(file2));
            Assert.Equal("b3d5cf638ed2f6a94d6b3c628f946196", Utils.File.GetShortMd5Hash(file2));

            // Delete the folders we created
            if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        }
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void LongFileNameTest()
    {
        const string testPath =
            @"\\nas4\assets4\_TV\Paw Patrol {tvdb-272472}\Season 7\Paw Patrol s07e01-e04 Mighty Pups, Charged Up Pups Stop a Humdinger Horde + Mighty Pups, Charged Up Pups Save a Mighty Lighthouse + Pups Save Election Day + Pups Save the Bubble Monkeys [HDTV-1080p][AAC 2.0][x264].mkv";
        Assert.True(testPath.Length > Utils.MAX_PATH);

        var path1 = Path.Combine(Path.GetTempPath(),
            @"FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\" +
            @"FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy" +
            @"FileCopy\FileCopy\FileCopy\FileCopy\FileCopy\FileCopy");
        Assert.True(path1.Length > 256);
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
        var file1 = Path.Combine(path1, "test1.txt");
        var file2 = Path.Combine(path1, "test2.txt");
        Utils.File.Create(file1);
        Assert.True(File.Exists(file1));
        Assert.False(File.Exists(file2));

        _ = Assert.Throws<NotSupportedException>(() =>
        {
            var ct = new CancellationToken();
            return Utils.File.Copy(file1, file2, ct);
        });
        Assert.True(File.Exists(file1));
        Assert.False(File.Exists(file2));

        // Delete the folders we created
        if (Directory.Exists(path1)) _ = Utils.Directory.Delete(path1, true);
    }
}