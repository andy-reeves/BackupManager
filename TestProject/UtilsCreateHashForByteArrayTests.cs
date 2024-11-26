// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsCreateHashForByteArrayTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsCreateHashForByteArrayTests
{
    [Fact]
    public void CreateHashForByteArray()
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\TestFile1");
        var size = new FileInfo(path).Length;
        var startDownloadPositionForEndBlock = size - Utils.END_BLOCK_SIZE;
        var startDownloadPositionForMiddleBlock = size / 2;
        var firstByteArray = Utils.File.GetByteArray(path, 0, Utils.START_BLOCK_SIZE);
        var secondByteArray = Utils.File.GetByteArray(path, startDownloadPositionForMiddleBlock, Utils.MIDDLE_BLOCK_SIZE);
        var thirdByteArray = Utils.File.GetByteArray(path, startDownloadPositionForEndBlock, Utils.END_BLOCK_SIZE);
        Assert.Equal("1416d38415ac751620b97eab7f433723", Utils.CreateHashForByteArray(firstByteArray, secondByteArray, thirdByteArray));
    }

    [Fact]
    public void CreateHashForByteArray2()
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\TestFile1");
        var size = new FileInfo(path).Length;
        var startDownloadPositionForMiddleBlock = size / 2;
        var firstByteArray = Utils.File.GetByteArray(path, 0, Utils.START_BLOCK_SIZE);
        var secondByteArray = Utils.File.GetByteArray(path, startDownloadPositionForMiddleBlock, Utils.MIDDLE_BLOCK_SIZE);
        Assert.Equal("3c99833946465953d07414d8739e9193", Utils.CreateHashForByteArray(firstByteArray, secondByteArray));
    }

    [Fact]
    public void CreateHashForByteArray3()
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\TestFile1");
        var firstByteArray = Utils.File.GetByteArray(path, 0, Utils.START_BLOCK_SIZE);
        Assert.Equal("e3dbe5f2844b142499cba55de6c429ce", Utils.CreateHashForByteArray(firstByteArray, null));
    }
}
