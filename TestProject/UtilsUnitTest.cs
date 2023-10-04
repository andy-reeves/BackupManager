using BackupManager;

namespace TestProject;

public class UtilsUnitTest
{
    [Fact]
    public void FormatTimeSpanFromSeconds()
    {
        var a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 300));
        Assert.True(a == "5 minutes");

        a = Utils.FormatTimeSpan(new TimeSpan(0, 0, 90000));
        Assert.True(a == "a day or so");
    }

    [Fact]
    public void FormatTimeFromSeconds()
    {
        var a = Utils.FormatTimeFromSeconds(300);
        Assert.True(a == "5 minutes");

        a = Utils.FormatTimeFromSeconds(100);
        Assert.True(a == "100 seconds");

        a = Utils.FormatTimeFromSeconds(306);
        Assert.True(a == "5 minutes");

        a = Utils.FormatTimeFromSeconds(3900);
        Assert.True(a == "1 hour");

        a = Utils.FormatTimeFromSeconds(90000);
        Assert.True(a == "a day or so");
    }
}