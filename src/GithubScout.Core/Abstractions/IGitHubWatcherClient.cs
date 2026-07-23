using GithubScout.Core.Models;

namespace GithubScout.Core.Abstractions;

/// <summary>Fetches the current state of a single watched PR or branch from GitHub.</summary>
public interface IGitHubWatcherClient
{
    Task<WatchStatus> GetStatusAsync(WatchItem item, CancellationToken cancellationToken);
}
