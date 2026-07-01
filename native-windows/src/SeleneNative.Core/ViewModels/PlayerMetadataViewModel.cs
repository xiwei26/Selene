using SeleneNative.Core.Models;
using SeleneNative.Core.Services;

namespace SeleneNative.Core.ViewModels;

public sealed class PlayerMetadataViewModel
{
    public SearchResult? Result { get; private set; }
    public TmdbBackdrop? TmdbBackdrop { get; private set; }
    public DoubanQuickInfo? QuickInfo { get; private set; }
    public List<DoubanComment> Comments { get; } = [];
    public List<DoubanRecommendation> Recommendations { get; } = [];
    public string? Overview { get; private set; }
    public bool IsLoading { get; private set; }

    public async Task LoadAsync(
        SearchResult result,
        IContentProvider? provider,
        CancellationToken cancellationToken = default)
    {
        Result = result;
        TmdbBackdrop = null;
        QuickInfo = null;
        Comments.Clear();
        Recommendations.Clear();
        Overview = result.Description;
        IsLoading = true;

        try
        {
            if (provider is null)
            {
                return;
            }

            try
            {
                TmdbBackdrop = await provider.GetTmdbBackdropAsync(
                        result.Title,
                        result.Year,
                        InferTmdbType(result),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // TMDB enrichment is optional and should never block playback.
            }

            try
            {
                QuickInfo = await provider.GetDoubanQuickInfoAsync(result.Title, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Douban quick-info is optional and should never block playback.
            }

            if (result.DoubanId is int doubanId && doubanId > 0)
            {
                try
                {
                    Comments.AddRange(await provider.GetDoubanCommentsAsync(
                            doubanId.ToString(),
                            cancellationToken)
                        .ConfigureAwait(false));
                }
                catch
                {
                    // Douban comments are optional.
                }

                try
                {
                    Recommendations.AddRange(await provider.GetDoubanRecommendationsAsync(
                            doubanId.ToString(),
                            cancellationToken)
                        .ConfigureAwait(false));
                }
                catch
                {
                    // Douban recommendations are optional.
                }
            }

            Overview = FirstNonEmpty(TmdbBackdrop?.Overview, QuickInfo?.Summary, result.Description);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static string InferTmdbType(SearchResult result)
    {
        var text = $"{result.Source} {result.TypeName} {result.ClassName}".ToLowerInvariant();
        return text.Contains("movie", StringComparison.Ordinal) ||
            text.Contains("电影", StringComparison.Ordinal)
            ? "movie"
            : "tv";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
