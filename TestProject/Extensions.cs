// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Extensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

#if DEBUG
using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class Extensions
{
    [Fact]
    public void Integer()
    {
        Assert.Equal("32nd", 32.ToOrdinalString());
        Assert.Equal("0", 0.ToOrdinalString());
    }

    [Fact]
    public void Control()
    {
        const string text = "test";
        var a = new TextBox();
        a.Invoke(static c => c.Text = text);
        Assert.Equal(text, a.Text);
    }

    [Fact]
    public void String()
    {
        Assert.Equal("10th", "10TH".ToTitleCaseIgnoreOrdinals());
        Assert.Equal("Test", "test".Capitalize());
    }
}
#endif