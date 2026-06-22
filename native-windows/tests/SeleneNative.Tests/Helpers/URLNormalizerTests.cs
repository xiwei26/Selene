using SeleneNative.Core.Helpers;
using Xunit;

namespace SeleneNative.Tests.Helpers;

public class URLNormalizerTests
{
    [Fact]
    public void NormalizeServerUrl_WithValidUrl_ShouldReturnNormalizedUrl()
    {
        // Arrange
        var url = "http://localhost:8080/";

        // Act
        var result = URLNormalizer.NormalizeServerUrl(url);

        // Assert
        Assert.Equal("http://localhost:8080", result);
    }

    [Fact]
    public void NormalizeServerUrl_WithoutProtocol_ShouldAddHttp()
    {
        // Arrange
        var url = "localhost:8080";

        // Act
        var result = URLNormalizer.NormalizeServerUrl(url);

        // Assert
        Assert.Equal("http://localhost:8080", result);
    }

    [Fact]
    public void NormalizeServerUrl_WithEmptyString_ShouldReturnDefault()
    {
        // Arrange
        var url = "";

        // Act
        var result = URLNormalizer.NormalizeServerUrl(url);

        // Assert
        Assert.Equal("http://localhost:8080", result);
    }

    [Fact]
    public void NormalizeServerUrl_WithHttps_ShouldPreserveProtocol()
    {
        // Arrange
        var url = "https://example.com";

        // Act
        var result = URLNormalizer.NormalizeServerUrl(url);

        // Assert
        Assert.Equal("https://example.com", result);
    }

    [Fact]
    public void NormalizeImageUrl_WithRelativeUrl_ShouldConvertToAbsolute()
    {
        // Arrange
        var url = "/images/poster.jpg";
        var baseUrl = "http://example.com";

        // Act
        var result = URLNormalizer.NormalizeImageUrl(url, baseUrl);

        // Assert
        Assert.Equal("http://example.com/images/poster.jpg", result);
    }

    [Fact]
    public void NormalizeImageUrl_WithAbsoluteUrl_ShouldReturnSame()
    {
        // Arrange
        var url = "https://cdn.example.com/poster.jpg";
        var baseUrl = "http://example.com";

        // Act
        var result = URLNormalizer.NormalizeImageUrl(url, baseUrl);

        // Assert
        Assert.Equal("https://cdn.example.com/poster.jpg", result);
    }

    [Fact]
    public void ExtractDomain_WithValidUrl_ShouldReturnDomain()
    {
        // Arrange
        var url = "http://example.com/path";

        // Act
        var result = URLNormalizer.ExtractDomain(url);

        // Assert
        Assert.Equal("example.com", result);
    }

    [Fact]
    public void IsValidUrl_WithValidUrl_ShouldReturnTrue()
    {
        // Arrange
        var url = "http://example.com";

        // Act
        var result = URLNormalizer.IsValidUrl(url);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidUrl_WithInvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var url = "not a url";

        // Act
        var result = URLNormalizer.IsValidUrl(url);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void AddQueryParameter_WithExistingQuery_ShouldAppendParameter()
    {
        // Arrange
        var url = "http://example.com/path?existing=value";
        var key = "new";
        var value = "test";

        // Act
        var result = URLNormalizer.AddQueryParameter(url, key, value);

        // Assert
        Assert.Contains("existing=value", result);
        Assert.Contains("new=test", result);
    }

    [Fact]
    public void AddQueryParameter_WithoutExistingQuery_ShouldAddParameter()
    {
        // Arrange
        var url = "http://example.com/path";
        var key = "new";
        var value = "test";

        // Act
        var result = URLNormalizer.AddQueryParameter(url, key, value);

        // Assert
        Assert.Equal("http://example.com/path?new=test", result);
    }
}
