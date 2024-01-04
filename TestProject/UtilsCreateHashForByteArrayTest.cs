// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="UtilsCreateHashForByteArrayTest.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
global using Xunit;

using System.Diagnostics.CodeAnalysis;

using BackupManager;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class UtilsCreateHashForByteArrayTest
{
    [Fact]
    public void CreateHashForByteArray()
    {
        var path = Path.Combine(Utils.GetProjectPath(typeof(UtilsUnitTests)), @"TestData\TestFile1");
        var size = new FileInfo(path).Length;
        var startDownloadPositionForEndBlock = size - Utils.EndBlockSize;
        var startDownloadPositionForMiddleBlock = size / 2;
        var firstByteArray = Utils.GetLocalFileByteArray(path, 0, Utils.StartBlockSize);
        var secondByteArray = Utils.GetLocalFileByteArray(path, startDownloadPositionForMiddleBlock, Utils.MiddleBlockSize);
        var thirdByteArray = Utils.GetLocalFileByteArray(path, startDownloadPositionForEndBlock, Utils.EndBlockSize);

        Assert.Equal("1416d38415ac751620b97eab7f433723",
            Utils.CreateHashForByteArray(firstByteArray, secondByteArray, thirdByteArray));
    }
}
#endif