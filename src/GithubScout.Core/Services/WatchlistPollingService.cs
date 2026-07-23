using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GithubScout.Core.Configuration;

namespace GithubScout.Core.Services;

/// <summary>
/// Periodically polls every watched item, detects changes against the last known snapshot,
/// updates the shared state repository and fans out notifications for anything that changed.
/// </summary>
public sealed class WatchlistPollingService : BackgroundService
{
    private readonly IWatchlistStore _watchlistStore;
    private readonly IGitHubWatcherClient _gitHubClient;
    private readonly IWatchStateRepository _stateRepository;
    private readonly IEnumerable<INotifier> _notifiers;
    private readonly GithubScoutOptions _options;
    private readonly ILogger<WatchlistPollingService> _logger;

    public WatchlistPollingService(
        IWatchlistStore watchlistStore,
        IGitHubWatcherClient gitHubClient,
        IWatchStateRepository stateRepository,
        IEnumerable<INotifier> notifiers,
        IOptions<GithubScoutOptions> options,
        ILogger<WatchlistPollingService> logger)
    {
        _watchlistStore = watchlistStore;
        _gitHubClient = gitHubClient;
        _stateRepository = stateRepository;
        _notifiers = notifiers;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    internal async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Models.WatchItem> items;
        try
        {
            items = await _watchlistStore.GetAllAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load the watchlist");
            return;
        }

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var previous = _stateRepository.GetStatus(item.Id);
                var current = await _gitHubClient.GetStatusAsync(item, cancellationToken);
                _stateRepository.UpdateStatus(item, current);

                var changes = ChangeDetector.Detect(previous, current);
                if (changes.Count == 0)
                {
                    continue;
                }

                foreach (var notifier in _notifiers)
                {
                    await notifier.NotifyAsync(item, current, changes, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to poll {Item}", item.DisplayName);
            }
        }
    }
}
