using System.Text.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface ISessionStore
{
    LoginSession? Current { get; }
    Task<LoginSession?> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LoginSession session, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class SessionStore : ISessionStore
{
    private readonly string _filePath;

    public SessionStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeleneNative",
            "session.json");
    }

    public LoginSession? Current { get; private set; }

    public async Task<LoginSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_filePath);
        Current = await JsonSerializer.DeserializeAsync<LoginSession>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return Current;
    }

    public async Task SaveAsync(LoginSession session, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, session, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        Current = session;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Current = null;
        if (File.Exists(_filePath))
        {
            File.Delete(_filePath);
        }

        return Task.CompletedTask;
    }
}
