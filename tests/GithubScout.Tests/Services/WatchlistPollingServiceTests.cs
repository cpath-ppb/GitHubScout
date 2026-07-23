using GithubScout.Core.Abstractions;
using GithubScout.Core.Configuration;
using GithubScout.Core.Models;
using GithubScout.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GithubScout.Tests.Services;

public class WatchlistPollingServiceTests
{
    private static WatchItem CreateItem() => new()
    {
        Id = "dotnet/runtime/pr/1",
        Owner = "dotnet",
        Repo = "runtime",
        Kind = WatchItemKind.PullRequest,
        PullRequestNumber = 1
    };

    private static WatchStatus CreateStatus(string state = "open") => new()
    {
        WatchItemId = "dotnet/runtime/pr/1",
        Title = "title",
        State = state,
        HtmlUrl = "https://github.com/dotnet/runtime/pull/1",
        CheckedAtUtc = DateTimeOffset.UtcNow
    };

    private static WatchlistPollingService CreateSut(
        Mock<IWatchlistStore> watchlistStore,
        Mock<IGitHubWatcherClient> gitHubClient,
        Mock<IWatchStateRepository> stateRepository,
        IEnumerable<INotifier> notifiers) => new(
            watchlistStore.Object,
            gitHubClient.Object,
            stateRepository.Object,
            notifiers,
            Options.Create(new GithubScoutOptions()),
            NullLogger<WatchlistPollingService>.Instance);

    [Fact]
    public async Task PollOnceAsync_WhenItemChanged_UpdatesStateAndNotifies()
    {
        var item = CreateItem();
        var status = CreateStatus();

        var watchlistStore = new Mock<IWatchlistStore>();
        watchlistStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { item });

        var gitHubClient = new Mock<IGitHubWatcherClient>();
        gitHubClient.Setup(c => c.GetStatusAsync(item, It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var stateRepository = new Mock<IWatchStateRepository>();
        stateRepository.Setup(r => r.GetStatus(item.Id)).Returns((WatchStatus?)null);

        var notifier = new Mock<INotifier>();

        var sut = CreateSut(watchlistStore, gitHubClient, stateRepository, new[] { notifier.Object });

        await sut.PollOnceAsync(CancellationToken.None);

        stateRepository.Verify(r => r.UpdateStatus(item, status), Times.Once);
        notifier.Verify(n => n.NotifyAsync(item, status, It.Is<IReadOnlyList<string>>(c => c.Count > 0), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PollOnceAsync_WhenNothingChanged_DoesNotNotify()
    {
        var item = CreateItem();
        var status = CreateStatus();

        var watchlistStore = new Mock<IWatchlistStore>();
        watchlistStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { item });

        var gitHubClient = new Mock<IGitHubWatcherClient>();
        gitHubClient.Setup(c => c.GetStatusAsync(item, It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var stateRepository = new Mock<IWatchStateRepository>();
        stateRepository.Setup(r => r.GetStatus(item.Id)).Returns(status);

        var notifier = new Mock<INotifier>();

        var sut = CreateSut(watchlistStore, gitHubClient, stateRepository, new[] { notifier.Object });

        await sut.PollOnceAsync(CancellationToken.None);

        notifier.Verify(n => n.NotifyAsync(It.IsAny<WatchItem>(), It.IsAny<WatchStatus>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PollOnceAsync_WhenClientThrows_ContinuesWithOtherItems()
    {
        var failingItem = CreateItem() with { Id = "dotnet/runtime/pr/1" };
        var workingItem = CreateItem() with { Id = "dotnet/runtime/pr/2", PullRequestNumber = 2 };
        var status = CreateStatus();

        var watchlistStore = new Mock<IWatchlistStore>();
        watchlistStore.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { failingItem, workingItem });

        var gitHubClient = new Mock<IGitHubWatcherClient>();
        gitHubClient.Setup(c => c.GetStatusAsync(failingItem, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        gitHubClient.Setup(c => c.GetStatusAsync(workingItem, It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var stateRepository = new Mock<IWatchStateRepository>();
        stateRepository.Setup(r => r.GetStatus(It.IsAny<string>())).Returns((WatchStatus?)null);

        var notifier = new Mock<INotifier>();

        var sut = CreateSut(watchlistStore, gitHubClient, stateRepository, new[] { notifier.Object });

        await sut.PollOnceAsync(CancellationToken.None);

        stateRepository.Verify(r => r.UpdateStatus(workingItem, status), Times.Once);
        stateRepository.Verify(r => r.UpdateStatus(failingItem, It.IsAny<WatchStatus>()), Times.Never);
    }
}
