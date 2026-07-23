using GithubScout.Core.Models;
using GithubScout.Core.Store;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GithubScout.Tests.Store;

public class JsonFileWatchlistStoreTests : IDisposable
{
    private readonly string _tempFile = Path.Combine(Path.GetTempPath(), $"githubscout-tests-{Guid.NewGuid():N}.json");

    private JsonFileWatchlistStore CreateSut() => new(_tempFile, NullLogger<JsonFileWatchlistStore>.Instance);

    [Fact]
    public async Task GetAllAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        var sut = CreateSut();

        var items = await sut.GetAllAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task AddAsync_ThenGetAllAsync_RoundTripsTheItem()
    {
        var sut = CreateSut();
        var item = new WatchItem
        {
            Id = "dotnet/runtime/pr/1",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.PullRequest,
            PullRequestNumber = 1
        };

        await sut.AddAsync(item);
        var items = await sut.GetAllAsync();

        var stored = Assert.Single(items);
        Assert.Equal(item.Id, stored.Id);
        Assert.Equal(item.Owner, stored.Owner);
        Assert.Equal(WatchItemKind.PullRequest, stored.Kind);
    }

    [Fact]
    public async Task AddAsync_WithExistingId_ReplacesTheItem()
    {
        var sut = CreateSut();
        var item = new WatchItem
        {
            Id = "dotnet/runtime/pr/1",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.PullRequest,
            PullRequestNumber = 1,
            Alias = "first"
        };
        await sut.AddAsync(item);

        await sut.AddAsync(item with { Alias = "second" });
        var items = await sut.GetAllAsync();

        var stored = Assert.Single(items);
        Assert.Equal("second", stored.Alias);
    }

    [Fact]
    public async Task RemoveAsync_WhenItemExists_RemovesItAndReturnsTrue()
    {
        var sut = CreateSut();
        var item = new WatchItem
        {
            Id = "dotnet/runtime/pr/1",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.PullRequest,
            PullRequestNumber = 1
        };
        await sut.AddAsync(item);

        var removed = await sut.RemoveAsync(item.Id);
        var items = await sut.GetAllAsync();

        Assert.True(removed);
        Assert.Empty(items);
    }

    [Fact]
    public async Task RemoveAsync_WhenItemDoesNotExist_ReturnsFalse()
    {
        var sut = CreateSut();

        var removed = await sut.RemoveAsync("does-not-exist");

        Assert.False(removed);
    }

    [Fact]
    public async Task GetAllAsync_WhenFileIsCorrupt_ReturnsEmptyListWithoutThrowing()
    {
        await File.WriteAllTextAsync(_tempFile, "{ not valid json");
        var sut = CreateSut();

        var items = await sut.GetAllAsync();

        Assert.Empty(items);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }
}
