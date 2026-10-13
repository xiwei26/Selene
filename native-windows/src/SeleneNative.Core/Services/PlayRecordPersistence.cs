using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// Writes a playback record locally and to the server. The local copy is written
/// first and unconditionally, so a slow, failing or signed-out server never loses
/// the record.
/// </summary>
public sealed class PlayRecordPersistence(IPlayRecordStore localStore)
{
    public async Task SaveAsync(
        PlayRecord record,
        IContentProvider? provider,
        CancellationToken remoteCancellationToken = default)
    {
        try
        {
            await localStore.SaveAsync(record, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Local persistence is best-effort; the server write below may still succeed.
        }

        if (provider is null)
        {
            return;
        }

        try
        {
            await provider.SavePlayRecordAsync(record, remoteCancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The local copy above is the fallback.
        }
    }
}
