using Ypopup.Core.Settings;
using Xunit;

namespace Ypopup.Core.Tests.Settings;

public class SettingsValidatorTests
{
    [Theory]
    [InlineData("내이름", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ValidateDisplayName(string? name, bool expectedValid)
    {
        var result = SettingsValidator.ValidateDisplayName(name);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("50505", true)]
    [InlineData("1024", true)]
    [InlineData("65535", true)]
    [InlineData("1023", false)]
    [InlineData("65536", false)]
    [InlineData("0", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidatePort(string? portText, bool expectedValid)
    {
        var result = SettingsValidator.ValidatePort(portText, "TEST");
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public void ValidatePortsDiffer_WhenEqual_ReturnsFail()
    {
        var result = SettingsValidator.ValidatePortsDiffer(50505, 50505, "TCP", "UDP");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidatePortsDiffer_WhenDifferent_ReturnsSuccess()
    {
        var result = SettingsValidator.ValidatePortsDiffer(50505, 50506, "TCP", "UDP");
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("C:\\share", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ValidateShareFolderPath(string? path, bool expectedValid)
    {
        var result = SettingsValidator.ValidateShareFolderPath(path);
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Theory]
    [InlineData("5", true)]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateAwayIdleMinutes(string? minutesText, bool expectedValid)
    {
        var result = SettingsValidator.ValidateAwayIdleMinutes(minutesText);
        Assert.Equal(expectedValid, result.IsValid);
    }
}
