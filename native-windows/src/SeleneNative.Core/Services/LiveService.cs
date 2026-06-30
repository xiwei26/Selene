using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Live source/channel/EPG provider abstraction.
/// Mirrors <c>LiveProviding</c> in the macOS client.
/// </summary>
public interface ILiveService
{
    Task<IReadOnlyList<LiveSource>> GetSourcesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LiveChannel>> GetChannelsAsync(string sourceKey, CancellationToken cancellationToken = default);
    Task<EpgData?> GetEpgAsync(string tvgId, string sourceKey, CancellationToken cancellationToken = default);
}

public sealed class ServerLiveService : ILiveService
{
    private readonly IContentProvider _provider;

    public ServerLiveService(IContentProvider provider)
    {
        _provider = provider;
    }

    public Task<IReadOnlyList<LiveSource>> GetSourcesAsync(CancellationToken cancellationToken = default)
    {
        return _provider.GetLiveSourcesAsync(cancellationToken);
    }

    public Task<IReadOnlyList<LiveChannel>> GetChannelsAsync(string sourceKey, CancellationToken cancellationToken = default)
    {
        return _provider.GetLiveChannelsAsync(sourceKey, cancellationToken);
    }

    public Task<EpgData?> GetEpgAsync(string tvgId, string sourceKey, CancellationToken cancellationToken = default)
    {
        return _provider.GetLiveEpgAsync(tvgId, sourceKey, cancellationToken);
    }
}
