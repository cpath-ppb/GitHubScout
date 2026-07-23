using GithubScout.Core.Models;

namespace GithubScout.Core.Abstractions;

/// <summary>
/// Persists the watchlist to durable storage (a JSON file by default) so it can be
/// hand-edited and also managed live from the web dashboard.
/// </summary>
public interface IWatchlistStore
{
    Task<IReadOnlyList<WatchItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<WatchItem> AddAsync(WatchItem item, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(string id, CancellationToken cancellationToken = default);
}
