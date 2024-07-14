// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="ConcurrentSetTests.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

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
        var bob = set.GetEnumerator();
        Assert.True(bob.MoveNext());
        bob.Dispose();
        var bob2 = (IEnumerable)set;
        using var enumerator = bob2.GetEnumerator() as IDisposable;
        var item4 = new FileSystemEntry(@"c:\testitem4", DateTime.MinValue);
        var set2 = (ICollection<FileSystemEntry>)set;
        set2.Add(item4);
        Assert.True(set2.Contains(item4));
        Assert.Equal(3, set2.Count);
        _ = Assert.Throws<ArgumentException>(() => set2.Add(item4));
        set.Clear();
        Assert.Empty(set);
        Assert.False(set2.Contains(item4));
    }

    [Fact]
    [SuppressMessage("ReSharper", "StringLiteralTypo")]
    public void ConcurrentHashSetTest()
    {
        var item1 = new FileSystemEntry(@"c:\testitem1");
        var item2 = new FileSystemEntry(@"c:\testitem2");
        var item3 = new FileSystemEntry(@"c:\testitem1", DateTime.MinValue);
        var set = new ConcurrentHashSet<FileSystemEntry>(new[] { item1, item2, item3 });
        Assert.Equal(2, set.Count);
        Assert.True(set.AddOrUpdate(item1));
        Assert.True(set.AddOrUpdate(item2));
        Assert.True(set.AddOrUpdate(item3));
        Assert.Equal(2, set.Count);

        foreach (var entry in set.Where(static entry => entry.Path == @"c:\testitem1"))
        {
            Assert.Equal(DateTime.MinValue, entry.ModifiedDateTime);
        }
        var bob = set.GetEnumerator();
        Assert.True(bob.MoveNext());
        bob.Dispose();
        var bob2 = (IEnumerable)set;
        using var enumerator = bob2.GetEnumerator() as IDisposable;
        var item4 = new FileSystemEntry(@"c:\testitem4", DateTime.MinValue);
        _ = set.AddOrUpdate(item4);
        Assert.True(set.Contains(item4));
        Assert.Equal(3, set.Count);
        Assert.True(set.Remove(item3));
        Assert.Equal(2, set.Count);
        set.Clear();
        Assert.Empty(set);
        Assert.False(set.Contains(item4));
        _ = set.Add(item3);
        set.Dispose();
        var finalizer = typeof(ConcurrentHashSet<FileSystemEntry>).GetMethod("Finalize", BindingFlags.Instance | BindingFlags.NonPublic);
        _ = finalizer?.Invoke(set, null);
        var bob3 = item1.ToString();
        Assert.Equal(@"c:\testitem1", bob3);
        Assert.False(item1.Equals((object)item2));
    }
}