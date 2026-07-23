using GithubScout.Core.Models;

namespace GithubScout.Core.Abstractions;

/// <summary>Notifies the outside world (toast, log, etc.) that a watched item changed.</summary>
public interface INotifier
{
    Task NotifyAsync(WatchItem item, WatchStatus currentStatus, IReadOnlyList<string> changes, CancellationToken cancellationToken);
}
