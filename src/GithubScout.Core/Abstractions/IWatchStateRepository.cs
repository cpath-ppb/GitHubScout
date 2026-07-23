using GithubScout.Core.Models;

namespace GithubScout.Core.Abstractions;

/// <summary>A single item's watch entry plus its most recently observed status, if any.</summary>
public sealed record WatchEntry(WatchItem Item, WatchStatus? Status);

/// <summary>
/// In-memory, thread-safe snapshot of the latest known status of every watched item plus a
/// rolling activity feed. Read by the web API and the console dashboard; written by the poller.
/// </summary>
public interface IWatchStateRepository
{
    void UpdateStatus(WatchItem item, WatchStatus status);

    void RemoveItem(string id);

    WatchStatus? GetStatus(string id);

    IReadOnlyList<WatchEntry> GetAll();

    void AddActivity(ActivityEvent activityEvent);

    IReadOnlyList<ActivityEvent> GetRecentActivity(int count = 50);
}
