using GithubScout.Core.Models;
using GithubScout.Core.Store;
using Xunit;

namespace GithubScout.Tests.Store;

public class InMemoryWatchStateRepositoryTests
{
    private static WatchItem CreateItem(string id = "dotnet/runtime/pr/1") => new()
    {
        Id = id,
        Owner = "dotnet",
        Repo = "runtime",
        Kind = WatchItemKind.PullRequest,
        PullRequestNumber = 1
    };

    private static WatchStatus CreateStatus(string id = "dotnet/runtime/pr/1") => new()
    {
        WatchItemId = id,
        Title = "title",
        State = "open",
        HtmlUrl = "https://github.com/dotnet/runtime/pull/1",
        CheckedAtUtc = DateTimeOffset.UtcNow
    };

    [Fact]
    public void UpdateStatus_ThenGetStatus_ReturnsStoredStatus()
    {
        var sut = new InMemoryWatchStateRepository();
        var item = CreateItem();
        var status = CreateStatus();

        sut.UpdateStatus(item, status);

        Assert.Equal(status, sut.GetStatus(item.Id));
    }

    [Fact]
    public void GetStatus_WhenNotTracked_ReturnsNull()
    {
        var sut = new InMemoryWatchStateRepository();

        Assert.Null(sut.GetStatus("unknown"));
    }

    [Fact]
    public void RemoveItem_RemovesTrackedStatus()
    {
        var sut = new InMemoryWatchStateRepository();
        var item = CreateItem();
        sut.UpdateStatus(item, CreateStatus());

        sut.RemoveItem(item.Id);

        Assert.Null(sut.GetStatus(item.Id));
    }

    [Fact]
    public void GetAll_ReturnsAllTrackedEntries()
    {
        var sut = new InMemoryWatchStateRepository();
        sut.UpdateStatus(CreateItem("a"), CreateStatus("a"));
        sut.UpdateStatus(CreateItem("b"), CreateStatus("b"));

        var all = sut.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void AddActivity_WhenExceedingMax_DropsOldestEntries()
    {
        var sut = new InMemoryWatchStateRepository(maxActivityEntries: 3);

        for (var i = 0; i < 5; i++)
        {
            sut.AddActivity(new ActivityEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow.AddSeconds(i),
                WatchItemId = "a",
                WatchItemDisplayName = "a",
                Message = $"event-{i}"
            });
        }

        var recent = sut.GetRecentActivity(10);

        Assert.Equal(3, recent.Count);
    }

    [Fact]
    public void GetRecentActivity_ReturnsNewestFirst()
    {
        var sut = new InMemoryWatchStateRepository();
        var older = DateTimeOffset.UtcNow.AddMinutes(-1);
        var newer = DateTimeOffset.UtcNow;

        sut.AddActivity(new ActivityEvent { TimestampUtc = older, WatchItemId = "a", WatchItemDisplayName = "a", Message = "old" });
        sut.AddActivity(new ActivityEvent { TimestampUtc = newer, WatchItemId = "a", WatchItemDisplayName = "a", Message = "new" });

        var recent = sut.GetRecentActivity(10);

        Assert.Equal("new", recent[0].Message);
    }
}
