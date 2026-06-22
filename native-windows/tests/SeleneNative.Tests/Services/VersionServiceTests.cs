using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Services;

public sealed class VersionServiceTests
{
    [Theory]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("1.10.0", "1.9.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.0-beta", "1.0.0", false)]
    public void IsRemoteVersionNewer_ShouldCompareCorrectly(string remote, string current, bool expected)
    {
        Assert.Equal(expected, VersionService.IsRemoteVersionNewer(remote, current));
    }
}
