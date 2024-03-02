// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ConcurrentSetTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;

using BackupManager;
using BackupManager.Entities;

namespace TestProject;

[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
public sealed class ConcurrentSetTests
{
    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void ConcurrentSetTest()
    {
        var item1 = new FileSystemEntry(@"c:\testitem1");
        var item2 = new FileSystemEntry(@"c:\testitem2");
        var item3 = new FileSystemEntry(@"c:\testitem1", DateTime.MinValue);
        var set = new ConcurrentSet<FileSystemEntry>(new[] { item1, item2, item3 });
        Assert.Equal(2, set.Count);
        Assert.True(set.AddOrUpdate(item1));
        Assert.True(set.AddOrUpdate(item2));
        Assert.True(set.AddOrUpdate(item3));
        Assert.Equal(2, set.Count);
        Assert.False(set.IsEmpty);

        foreach (var entry in set.Where(static entry => entry.Path == @"c:\testitem1"))
        {
            Assert.Equal(DateTime.MinValue, entry.ModifiedDateTime);
        }
    }
}