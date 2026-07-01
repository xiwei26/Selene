using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Player;

public sealed class PlayerMetadataViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShouldFetchTmdbAndDoubanMetadata()
    {
        var provider = new CapturingContentProvider();
        var result = new SearchResult
        {
            Title = "Cang Yuan Tu",
            Year = "2023",
            Source = "tv",
            SourceName = "TV",
            Description = "Seed description",
            DoubanId = 12345,
        };
        var vm = new PlayerMetadataViewModel();

        await vm.LoadAsync(result, provider);

        Assert.Equal(("Cang Yuan Tu", "2023", "tv"), provider.TmdbRequest);
        Assert.Equal("Cang Yuan Tu", provider.QuickInfoTitle);
        Assert.Equal("12345", provider.CommentsDoubanId);
        Assert.Equal("12345", provider.RecommendationsDoubanId);
        Assert.Equal("TMDB overview", vm.Overview);
        Assert.Equal("8.7", vm.QuickInfo?.Rating);
        Assert.Single(vm.Comments);
        Assert.Single(vm.Recommendations);
        Assert.False(vm.IsLoading);
    }

    private sealed class CapturingContentProvider : IContentProvider
    {
        public (string Title, string? Year, string? Type)? TmdbRequest { get; private set; }
        public string? QuickInfoTitle { get; private set; }
        public string? CommentsDoubanId { get; private set; }
        public string? RecommendationsDoubanId { get; private set; }

        public Task<LoginSession> LoginAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SearchResult>> SearchAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResult>>([]);

        public Task<SearchResult?> DetailAsync(
            string source,
            string id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<SearchResult?>(null);

        public Task<IReadOnlyList<SearchResource>> SearchResourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchResource>>([]);

        public Task<IReadOnlyList<FavoriteItem>> GetFavoritesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FavoriteItem>>([]);

        public Task AddFavoriteAsync(
            string source,
            string id,
            Dictionary<string, object> data,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RemoveFavoriteAsync(string source, string id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PlayRecord>> GetPlayRecordsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlayRecord>>([]);

        public Task SavePlayRecordAsync(PlayRecord record, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeletePlayRecordAsync(string source, string id, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearPlayRecordsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<string>> GetSearchHistoryAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task AddSearchHistoryAsync(string query, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteSearchHistoryAsync(string query, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<SearchSuggestion>> SearchSuggestionsAsync(
            string query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SearchSuggestion>>([]);

        public Task<IReadOnlyList<LiveSource>> GetLiveSourcesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LiveSource>>([]);

        public Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(
            string sourceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LiveChannel>>([]);

        public Task<EpgData?> GetLiveEpgAsync(
            string tvgId,
            string sourceKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EpgData?>(null);

        public Task<TmdbBackdrop?> GetTmdbBackdropAsync(
            string title,
            string? year = null,
            string? type = null,
            CancellationToken cancellationToken = default)
        {
            TmdbRequest = (title, year, type);
            return Task.FromResult<TmdbBackdrop?>(new TmdbBackdrop
            {
                Title = title,
                Overview = "TMDB overview",
                Rating = 8.1,
                Year = year,
                NumberOfSeasons = 2,
            });
        }

        public Task<DoubanQuickInfo?> GetDoubanQuickInfoAsync(
            string title,
            CancellationToken cancellationToken = default)
        {
            QuickInfoTitle = title;
            return Task.FromResult<DoubanQuickInfo?>(new DoubanQuickInfo
            {
                Title = title,
                Rating = "8.7",
                Summary = "Douban summary",
                Genres = ["Animation"],
                Directors = ["Director"],
                Cast = ["Actor"],
            });
        }

        public Task<IReadOnlyList<DoubanComment>> GetDoubanCommentsAsync(
            string doubanId,
            CancellationToken cancellationToken = default)
        {
            CommentsDoubanId = doubanId;
            return Task.FromResult<IReadOnlyList<DoubanComment>>(
            [
                new DoubanComment { Author = "User", Content = "Nice" }
            ]);
        }

        public Task<IReadOnlyList<DoubanRecommendation>> GetDoubanRecommendationsAsync(
            string doubanId,
            CancellationToken cancellationToken = default)
        {
            RecommendationsDoubanId = doubanId;
            return Task.FromResult<IReadOnlyList<DoubanRecommendation>>(
            [
                new DoubanRecommendation { Id = "r1", Title = "Related", Rating = "8.0" }
            ]);
        }
    }
}
