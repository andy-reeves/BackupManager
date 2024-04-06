// --------------------------------------------------------------------------------------------------------------------
//  <copyright file="Extensions.cs" company="Andy Reeves">
// 
//  </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.Windows.Forms;

using BackupManager.Extensions;

namespace TestProject;

[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
[SuppressMessage("ReSharper", "MemberCanBeFileLocal")]
public sealed class Extensions
{
    [InlineData("32nd", 32)]
    [InlineData("0", 0)]
    [InlineData("20th", 20)]
    [Theory]
    public void Integer(string expectedValue, int testValue)
    {
        Assert.Equal(expectedValue, testValue.ToOrdinalString());
    }

    [Fact]
    public void Control()
    {
        const string text = "test";
        var a = new TextBox();
        a.Invoke(static c => c.Text = text);
        Assert.Equal(text, a.Text);
    }

    [InlineData("2nd", "2nd", "2nd")]
    [InlineData("3rd", "3Rd", "3Rd")]
    [InlineData("10th", "10TH", "10TH")]
    [InlineData("Test", "Test", "test")]
    [Theory]
    public void String(string expectedValueOrdinal, string expectedValueCapitalize, string testValue)
    {
        Assert.Equal(expectedValueOrdinal, testValue.ToTitleCaseIgnoreOrdinals());
        Assert.Equal(expectedValueCapitalize, testValue.Capitalize());
    }
}
