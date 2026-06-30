using System.Runtime.CompilerServices;
using System.Text.Json;
using SeleneNative.Core.Models;

namespace SeleneNative.Core.Services;

/// <summary>
/// SSE streaming search client. Consumes the server's <c>/api/search/ws?q=...</c>
/// endpoint via <c>text/event-stream</c> and exposes three channels for the
/// ViewModel to observe: incremental results, progress, and errors.
/// Mirrors <c>SSESearchClient.swift</c> in the macOS client.
/// </summary>
public sealed class SSESearchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private CancellationTokenSource? _cts;
    private readonly HttpClient _httpClient;

    public SSESearchClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public event Action<IReadOnlyList<SearchResult>>? IncrementalResults;
    public event Action<SearchProgress>? Progress;
    public event Action<string>? Errors;

    public bool IsRunning => _cts is not null && !_cts.IsCancellationRequested;

    public async Task StartSearchAsync(
        string query,
        string serverUrl,
        string cookie = "",
        CancellationToken cancellationToken = default)
    {
        Stop();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        try
        {
            var url = $"{serverUrl.TrimEnd('/')}/api/search/ws?q={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("text/event-stream");
            request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
            {
                NoCache = true,
            };
            if (!string.IsNullOrWhiteSpace(cookie))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                Errors?.Invoke($"SSE 连接失败 ({(int)response.StatusCode}): {body}");
                Progress?.Invoke(new SearchProgress { IsComplete = true, Error = body });
                return;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            var currentEvent = string.Empty;
            var dataLines = new List<string>();

            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line is null)
                {
                    break; // stream ended
                }

                if (string.IsNullOrEmpty(line))
                {
                    // blank line flushes the event
                    if (!string.IsNullOrEmpty(currentEvent) && dataLines.Count > 0)
                    {
                        var data = string.Join("\n", dataLines);
                        HandleEvent(currentEvent, data);
                    }
                    currentEvent = string.Empty;
                    dataLines.Clear();
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
                {
                    currentEvent = line["event:".Length..].Trim();
                }
                else if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    dataLines.Add(line["data:".Length..].Trim());
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // expected on Stop()
        }
        catch (Exception ex)
        {
            Errors?.Invoke(ex.Message);
            Progress?.Invoke(new SearchProgress { IsComplete = true, Error = ex.Message });
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Stop()
    {
        if (_cts is not null)
        {
            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
            _cts = null;
        }
    }

    private void HandleEvent(string eventType, string data)
    {
        try
        {
            switch (eventType)
            {
                case "start":
                    HandleStart(data);
                    break;
                case "sourceResult":
                    HandleSourceResult(data);
                    break;
                case "sourceError":
                    HandleSourceError(data);
                    break;
                case "complete":
                    Progress?.Invoke(new SearchProgress { IsComplete = true });
                    break;
            }
        }
        catch (Exception ex)
        {
            Errors?.Invoke($"解析 SSE 事件失败: {ex.Message}");
        }
    }

    private void HandleStart(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var totalSources = ReadInt(root, "totalSources", "total_sources");
        Progress?.Invoke(new SearchProgress
        {
            TotalSources = totalSources,
            CompletedSources = 0,
        });
    }

    private void HandleSourceResult(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var sourceName = ReadString(root, "sourceName", "source_name");
        var results = new List<SearchResult>();

        if (root.TryGetProperty("results", out var resultsElement) &&
            resultsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in resultsElement.EnumerateArray())
            {
                var result = item.Deserialize<SearchResult>(JsonOptions);
                if (result is not null)
                {
                    results.Add(result);
                }
            }
        }

        if (results.Count > 0)
        {
            IncrementalResults?.Invoke(results);
        }

        Progress?.Invoke(new SearchProgress
        {
            CurrentSource = sourceName,
            CompletedSources = 1, // caller should accumulate
        });
    }

    private void HandleSourceError(string data)
    {
        using var doc = JsonDocument.Parse(data);
        var root = doc.RootElement;
        var sourceName = ReadString(root, "sourceName", "source_name");
        var error = ReadString(root, "error");
        Errors?.Invoke($"{sourceName}: {error}");
        Progress?.Invoke(new SearchProgress
        {
            CurrentSource = sourceName,
            CompletedSources = 1,
        });
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                return prop.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) &&
                prop.ValueKind == JsonValueKind.Number &&
                prop.TryGetInt32(out var value))
            {
                return value;
            }
        }
        return 0;
    }
}

public sealed class SearchProgress
{
    public int TotalSources { get; init; }
    public int CompletedSources { get; init; }
    public string? CurrentSource { get; init; }
    public bool IsComplete { get; init; }
    public string? Error { get; init; }

    public double ProgressPercentage => TotalSources <= 0
        ? (IsComplete ? 1 : 0)
        : Math.Clamp((double)CompletedSources / TotalSources, 0, 1);
}
