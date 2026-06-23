using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Services;

public sealed class CacheServiceTests
{
    [Fact]
    public async Task SaveAndLoad_ShouldRoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var svc = new CacheService(path);
        try
        {
            await svc.SaveAsync("key1", new { Name = "test" }, TimeSpan.FromMinutes(10));
            var result = await svc.LoadAsync<SimplePayload>("key1", TimeSpan.FromMinutes(10));
            Assert.NotNull(result);
            Assert.Equal("test", result?.Name);
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task Load_ShouldReturnNull_WhenExpired()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var svc = new CacheService(path);
        try
        {
            await svc.SaveAsync("key1", new { Name = "test" }, TimeSpan.FromMilliseconds(1));
            await Task.Delay(50);
            var result = await svc.LoadAsync<SimplePayload>("key1", TimeSpan.FromMilliseconds(1));
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    [Fact]
    public async Task ClearExpired_ShouldRemoveExpiredEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var svc = new CacheService(path);
        try
        {
            await svc.SaveAsync("fresh", new { Name = "a" }, TimeSpan.FromHours(1));
            await svc.SaveAsync("stale", new { Name = "b" }, TimeSpan.FromMilliseconds(1));
            await Task.Delay(50);
            await svc.ClearExpiredAsync();
            Assert.NotNull(await svc.LoadAsync<SimplePayload>("fresh", TimeSpan.FromHours(1)));
            Assert.Null(await svc.LoadAsync<SimplePayload>("stale", TimeSpan.FromHours(1)));
        }
        finally
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
    }

    private sealed record SimplePayload(string Name);
}
