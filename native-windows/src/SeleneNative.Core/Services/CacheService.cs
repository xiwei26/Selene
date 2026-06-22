using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SeleneNative.Core.Services;

/// <summary>
/// File-based JSON cache with per-entry TTL metadata sidecars.
/// Mirrors <c>CacheService.swift</c> in the macOS client.
/// </summary>
public interface ICacheService
{
    Task<T?> LoadAsync<T>(string key, TimeSpan maxAge, CancellationToken cancellationToken = default);
    Task SaveAsync<T>(string key, T data, TimeSpan maxAge, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearExpiredAsync(CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}

public sealed class CacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _root;
    private readonly string _namespace;

    public CacheService(string? rootPath = null, string ns = "default")
    {
        _root = rootPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeleneNative",
            "Cache");
        _namespace = ns;
    }

    public async Task<T?> LoadAsync<T>(string key, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var dataPath = DataPath(key);
        var metaPath = MetaPath(key);

        if (!File.Exists(dataPath) || !File.Exists(metaPath))
        {
            return default;
        }

        var meta = await ReadMetaAsync(metaPath, cancellationToken).ConfigureAwait(false);
        if (meta is null || DateTimeOffset.UtcNow - meta.SaveTime > meta.MaxAge)
        {
            await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            return default;
        }

        var json = await File.ReadAllTextAsync(dataPath, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public async Task SaveAsync<T>(string key, T data, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_root, _namespace);
        Directory.CreateDirectory(dir);

        var dataPath = DataPath(key);
        var metaPath = MetaPath(key);

        var json = JsonSerializer.Serialize(data, JsonOptions);
        await File.WriteAllTextAsync(dataPath, json, cancellationToken).ConfigureAwait(false);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(new CacheMeta
        {
            SaveTime = DateTimeOffset.UtcNow,
            MaxAge = maxAge,
        }, JsonOptions), cancellationToken).ConfigureAwait(false);
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var dataPath = DataPath(key);
        var metaPath = MetaPath(key);
        if (File.Exists(dataPath)) File.Delete(dataPath);
        if (File.Exists(metaPath)) File.Delete(metaPath);
        return Task.CompletedTask;
    }

    public async Task ClearExpiredAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_root, _namespace);
        if (!Directory.Exists(dir)) return;

        var now = DateTimeOffset.UtcNow;
        foreach (var metaFile in Directory.EnumerateFiles(dir, "*.meta"))
        {
            try
            {
                var meta = await ReadMetaAsync(metaFile, cancellationToken).ConfigureAwait(false);
                if (meta is not null && now - meta.SaveTime > meta.MaxAge)
                {
                    var key = Path.GetFileNameWithoutExtension(metaFile);
                    await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
                }
            }
            catch
            {
                // corrupt meta — delete both
                var key = Path.GetFileNameWithoutExtension(metaFile);
                await RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var dir = Path.Combine(_root, _namespace);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
        return Task.CompletedTask;
    }

    private string DataPath(string key) =>
        Path.Combine(_root, _namespace, $"{HashKey(key)}.json");

    private string MetaPath(string key) =>
        Path.Combine(_root, _namespace, $"{HashKey(key)}.meta");

    private static async Task<CacheMeta?> ReadMetaAsync(string path, CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<CacheMeta>(json, JsonOptions);
    }

    private static string HashKey(string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed record CacheMeta
    {
        public DateTimeOffset SaveTime { get; init; }
        public TimeSpan MaxAge { get; init; }
    }
}
