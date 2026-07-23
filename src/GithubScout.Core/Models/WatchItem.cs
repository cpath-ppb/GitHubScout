namespace GithubScout.Core.Models;

/// <summary>
/// A single repository entity (pull request or branch) that GithubScout polls periodically.
/// </summary>
public sealed record WatchItem
{
    public required string Id { get; init; }

    public required string Owner { get; init; }

    public required string Repo { get; init; }

    public required WatchItemKind Kind { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="WatchItemKind.Branch"/>.</summary>
    public string? BranchName { get; init; }

    /// <summary>Populated when <see cref="Kind"/> is <see cref="WatchItemKind.PullRequest"/>.</summary>
    public int? PullRequestNumber { get; init; }

    /// <summary>Optional friendly display name; falls back to a generated label when absent.</summary>
    public string? Alias { get; init; }

    public string RepositoryFullName => $"{Owner}/{Repo}";

    public string DisplayName => Alias ?? Kind switch
    {
        WatchItemKind.PullRequest => $"{RepositoryFullName}#{PullRequestNumber}",
        WatchItemKind.Branch => $"{RepositoryFullName}@{BranchName}",
        _ => RepositoryFullName
    };

    /// <summary>
    /// Builds a deterministic identifier from the repository coordinates so the same
    /// PR/branch always resolves to the same watchlist entry, even across restarts.
    /// </summary>
    public static string BuildId(string owner, string repo, WatchItemKind kind, string? branchName, int? pullRequestNumber) => kind switch
    {
        WatchItemKind.PullRequest => $"{owner}/{repo}/pr/{pullRequestNumber}".ToLowerInvariant(),
        WatchItemKind.Branch => $"{owner}/{repo}/branch/{branchName}".ToLowerInvariant(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };
}
