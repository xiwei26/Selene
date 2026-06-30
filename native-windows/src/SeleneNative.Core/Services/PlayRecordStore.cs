using System.Text.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

public interface IPlayRecordStore
{
    Task<IReadOnlyList<PlayRecord>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(PlayRecord record, CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<PlayRecord> records, CancellationToken cancellationToken = default);
}

public sealed class PlayRecordStore : IPlayRecordStore
{
    private readonly string _filePath;

    public PlayRecordStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SeleneNative",
            "playrecords.json");
    }

    public async Task<IReadOnlyList<PlayRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var records = document.RootElement.ValueKind switch
        {
            JsonValueKind.Array => ReadArray(document.RootElement),
            JsonValueKind.Object => ReadMap(document.RootElement),
            _ => []
        };

        return records
            .OrderByDescending(record => record.SaveTime)
            .ToList();
    }

    public async Task SaveAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        var existing = (await LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var key = string.IsNullOrEmpty(record.Id) ? $"{record.Source}+{record.ItemId}" : record.Id;
        var withKey = CloneWithId(record, key);
        var updated = existing.Where(r => !KeyMatches(r, key)).ToList();
        updated.Add(withKey);
        await SaveAllAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAllAsync(IEnumerable<PlayRecord> records, CancellationToken cancellationToken = default)
    {
        var ordered = records
            .OrderByDescending(r => r.SaveTime)
            .ToList();
        var map = new Dictionary<string, PlayRecord>(StringComparer.Ordinal);
        foreach (var record in ordered)
        {
            var key = string.IsNullOrEmpty(record.Id)
                ? $"{record.Source}+{record.ItemId}"
                : record.Id;
            map[key] = record;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, map, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static List<PlayRecord> ReadArray(JsonElement root)
    {
        return root.EnumerateArray()
            .Select(DeserializeRecord)
            .Where(record => record is not null)
            .Cast<PlayRecord>()
            .ToList();
    }

    private static List<PlayRecord> ReadMap(JsonElement root)
    {
        return root.EnumerateObject()
            .Select(property => DeserializeRecord(property.Value))
            .Where(record => record is not null)
            .Cast<PlayRecord>()
            .ToList();
    }

    private static PlayRecord? DeserializeRecord(JsonElement element)
    {
        try
        {
            return JsonSerializer.Deserialize<PlayRecord>(element.GetRawText());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool KeyMatches(PlayRecord record, string key)
    {
        if (!string.IsNullOrEmpty(record.Id) && record.Id == key)
        {
            return true;
        }

        return $"{record.Source}+{record.ItemId}" == key;
    }

    private static PlayRecord CloneWithId(PlayRecord record, string key)
    {
        return new PlayRecord
        {
            Title = record.Title,
            Source = record.Source,
            SourceName = record.SourceName,
            Id = key,
            Cover = record.Cover,
            Year = record.Year,
            EpisodeNumber = record.EpisodeNumber,
            TotalEpisodes = record.TotalEpisodes,
            PlayTime = record.PlayTime,
            TotalTime = record.TotalTime,
            SaveTime = record.SaveTime,
            SearchTitle = record.SearchTitle
        };
    }
}
