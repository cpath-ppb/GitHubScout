using GithubScout.Core.Models;

namespace GithubScout.Core.Services;

/// <summary>
/// Pure, side-effect-free comparison between two consecutive <see cref="WatchStatus"/> snapshots
/// of the same watch item. Kept dependency-free so it is trivial to unit test.
/// </summary>
public static class ChangeDetector
{
    public static IReadOnlyList<string> Detect(WatchStatus? previous, WatchStatus current)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (previous is null)
        {
            return new[] { "Started watching" };
        }

        var changes = new List<string>();

        if (!string.Equals(previous.State, current.State, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add($"State changed: {previous.State} -> {current.State}");
        }

        if (previous.ChecksState != current.ChecksState)
        {
            changes.Add($"Checks changed: {previous.ChecksState} -> {current.ChecksState}");
        }

        if (previous.ReviewState != current.ReviewState)
        {
            changes.Add($"Review changed: {previous.ReviewState} -> {current.ReviewState}");
        }
        else if (!previous.PendingReviewers.SequenceEqual(current.PendingReviewers) && current.PendingReviewers.Count > 0)
        {
            changes.Add($"Still awaiting review from: {string.Join(", ", current.PendingReviewers)}");
        }

        if (!string.Equals(previous.LatestCommitSha, current.LatestCommitSha, StringComparison.Ordinal)
            && !string.IsNullOrEmpty(current.LatestCommitSha))
        {
            var shortSha = current.LatestCommitSha.Length >= 7 ? current.LatestCommitSha[..7] : current.LatestCommitSha;
            changes.Add($"New commit pushed: {shortSha} {current.LatestCommitMessage}".TrimEnd());
        }

        if (previous.Mergeable != current.Mergeable && current.Mergeable is not null)
        {
            changes.Add(current.Mergeable.Value ? "No longer has merge conflicts" : "Now has merge conflicts");
        }

        return changes;
    }
}
