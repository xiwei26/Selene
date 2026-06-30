using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using Xunit;

namespace SeleneNative.Tests.Services;

public sealed class SessionStoreTests
{
    [Fact]
    public async Task SaveAndLoadAsync_ShouldRoundTripSession()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");
        var store = new SessionStore(filePath);

        await store.SaveAsync(new LoginSession
        {
            ServerUrl = "http://server.test",
            Username = "alice",
            Cookie = "sid=abc"
        });

        var reloaded = new SessionStore(filePath);
        var session = await reloaded.LoadAsync();

        Assert.NotNull(session);
        Assert.Equal("alice", session.Username);
        Assert.Equal("sid=abc", session.Cookie);

        await reloaded.ClearAsync();
        Assert.False(File.Exists(filePath));
    }
}
