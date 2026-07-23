namespace GithubScout.Core.Models;

public enum CheckState
{
    Unknown,
    NoChecks,
    Pending,
    Success,
    Failure
}

public enum ReviewState
{
    Unknown,
    NoReviewRequired,
    ReviewRequired,
    ChangesRequested,
    Approved
}

/// <summary>
/// A point-in-time snapshot of a watched PR or branch, as returned by <see cref="GithubScout.Core.Abstractions.IGitHubWatcherClient"/>.
/// </summary>
public sealed record WatchStatus
{
    public required string WatchItemId { get; init; }

    public required string Title { get; init; }

    public string? AuthorLogin { get; init; }

    /// <summary>"open", "draft", "merged", "closed" for PRs; "active" for branches.</summary>
    public required string State { get; init; }

    public CheckState ChecksState { get; init; } = CheckState.Unknown;

    public ReviewState ReviewState { get; init; } = ReviewState.Unknown;

    public string? LatestCommitSha { get; init; }

    public string? LatestCommitMessage { get; init; }

    public DateTimeOffset? LatestCommitAtUtc { get; init; }

    public bool? Mergeable { get; init; }

    /// <summary>
    /// Names of users/teams still requested to review but who haven't submitted a review yet
    /// (e.g. a pending CODEOWNERS team). Non-empty means the PR is likely still blocked even
    /// if other reviewers already approved it.
    /// </summary>
    public IReadOnlyList<string> PendingReviewers { get; init; } = Array.Empty<string>();

    public required string HtmlUrl { get; init; }

    public required DateTimeOffset CheckedAtUtc { get; init; }
}
