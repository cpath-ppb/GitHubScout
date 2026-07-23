using System.Collections.Concurrent;
using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;

namespace GithubScout.Core.Store;

/// <summary>Thread-safe in-memory implementation of <see cref="IWatchStateRepository"/>.</summary>
public sealed class InMemoryWatchStateRepository : IWatchStateRepository
{
    private readonly ConcurrentDictionary<string, WatchEntry> _entries = new();
    private readonly ConcurrentQueue<ActivityEvent> _activity = new();
    private readonly int _maxActivityEntries;

    public InMemoryWatchStateRepository(int maxActivityEntries = 200)
    {
        _maxActivityEntries = maxActivityEntries;
    }

    public void UpdateStatus(WatchItem item, WatchStatus status)
    {
        _entries[item.Id] = new WatchEntry(item, status);
    }

    public void RemoveItem(string id)
    {
        _entries.TryRemove(id, out _);
    }

    public WatchStatus? GetStatus(string id) => _entries.TryGetValue(id, out var entry) ? entry.Status : null;

    public IReadOnlyList<WatchEntry> GetAll() => _entries.Values
        .OrderBy(e => e.Item.RepositoryFullName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public void AddActivity(ActivityEvent activityEvent)
    {
        _activity.Enqueue(activityEvent);
        while (_activity.Count > _maxActivityEntries && _activity.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<ActivityEvent> GetRecentActivity(int count = 50) => _activity
        .OrderByDescending(e => e.TimestampUtc)
        .Take(count)
        .ToList();
}
