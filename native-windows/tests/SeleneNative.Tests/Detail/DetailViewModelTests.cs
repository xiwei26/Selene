using SeleneNative.Core.Models;
using SeleneNative.Core.Services;
using SeleneNative.Core.ViewModels;
using Xunit;

namespace SeleneNative.Tests.Detail;

public sealed class DetailViewModelTests
{
    [Fact]
    public async Task LoadAsync_ShouldPopulateEpisodesFromSeed()
    {
        var seed = new SearchResult
        {
            Title = "Test",
            Source = "src",
            Id = "1",
            Episodes = ["https://example.com/ep1.m3u8", "https://example.com/ep2.m3u8"],
            EpisodeTitles = ["第一集", "第二集"],
        };
        var vm = new DetailViewModel();

        await vm.LoadAsync(seed, null, null);

        Assert.Equal(2, vm.Episodes.Count);
        Assert.Equal("第一集", vm.EpisodeTitles[0]);
        Assert.Same(seed, vm.Result);
    }

    [Fact]
    public void PlayEpisodeCommand_ShouldNotFire_WhenIndexOutOfRange()
    {
        var vm = new DetailViewModel();
        string? firedUrl = null;
        vm.PlayRequested += (url, _) => firedUrl = url;

        vm.PlayEpisodeCommand.Execute(-1);
        Assert.Null(firedUrl);

        vm.PlayEpisodeCommand.Execute(0);
        Assert.Null(firedUrl);
    }

    [Fact]
    public void PlayEpisodeCommand_ShouldFire_WhenValid()
    {
        var vm = new DetailViewModel();
        vm.Episodes.Add("https://example.com/ep1.m3u8");
        vm.EpisodeTitles.Add("第一集");
        vm.Result = new SearchResult { Title = "T", Source = "s", Id = "1" };

        string? firedUrl = null;
        int firedIndex = -1;
        vm.PlayRequested += (url, index) =>
        {
            firedUrl = url;
            firedIndex = index;
        };

        vm.PlayEpisodeCommand.Execute(0);

        Assert.Equal("https://example.com/ep1.m3u8", firedUrl);
        Assert.Equal(0, firedIndex);
    }

    [Fact]
    public async Task LoadAsync_ShouldHandleDoubanFailure()
    {
        var seed = new SearchResult
        {
            Title = "Test",
            Source = "src",
            Id = "1",
            DoubanId = 123,
            Episodes = ["https://example.com/ep1.m3u8"],
        };
        var vm = new DetailViewModel();

        // DoubanClient not injected, so DoubanInfo stays null
        await vm.LoadAsync(seed, null, null);

        Assert.Null(vm.DoubanInfo);
        Assert.Null(vm.ErrorMessage);
        Assert.Single(vm.Episodes);
    }
}
