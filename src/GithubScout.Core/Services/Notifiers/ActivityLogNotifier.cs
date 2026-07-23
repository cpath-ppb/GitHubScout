using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.Notifiers;

/// <summary>
/// Always-active notifier that records changes into the shared <see cref="IWatchStateRepository"/>
/// activity feed (surfaced by both the console dashboard and the website) and the structured log.
/// </summary>
public sealed class ActivityLogNotifier : INotifier
{
    private readonly IWatchStateRepository _repository;
    private readonly ILogger<ActivityLogNotifier> _logger;

    public ActivityLogNotifier(IWatchStateRepository repository, ILogger<ActivityLogNotifier> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task NotifyAsync(WatchItem item, WatchStatus currentStatus, IReadOnlyList<string> changes, CancellationToken cancellationToken)
    {
        foreach (var change in changes)
        {
            _repository.AddActivity(new ActivityEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                WatchItemId = item.Id,
                WatchItemDisplayName = item.DisplayName,
                Message = change
            });

            _logger.LogInformation("[{Item}] {Change}", item.DisplayName, change);
        }

        return Task.CompletedTask;
    }
}
