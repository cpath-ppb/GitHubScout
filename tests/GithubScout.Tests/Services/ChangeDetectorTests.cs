using GithubScout.Core.Models;
using GithubScout.Core.Services;
using Xunit;

namespace GithubScout.Tests.Services;

public class ChangeDetectorTests
{
    private static WatchStatus CreateStatus(
        string state = "open",
        CheckState checksState = CheckState.Pending,
        ReviewState reviewState = ReviewState.ReviewRequired,
        string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
        bool? mergeable = true,
        IReadOnlyList<string>? pendingReviewers = null) => new()
        {
            WatchItemId = "owner/repo/pr/1",
            Title = "Some title",
            State = state,
            ChecksState = checksState,
            ReviewState = reviewState,
            LatestCommitSha = sha,
            LatestCommitMessage = "message",
            HtmlUrl = "https://github.com/owner/repo/pull/1",
            Mergeable = mergeable,
            PendingReviewers = pendingReviewers ?? Array.Empty<string>(),
            CheckedAtUtc = DateTimeOffset.UtcNow
        };

    [Fact]
    public void Detect_WhenNoPreviousSnapshot_ReturnsStartedWatching()
    {
        var current = CreateStatus();

        var changes = ChangeDetector.Detect(null, current);

        Assert.Single(changes);
        Assert.Contains("Started watching", changes[0]);
    }

    [Fact]
    public void Detect_WhenNothingChanged_ReturnsNoChanges()
    {
        var previous = CreateStatus();
        var current = CreateStatus();

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Empty(changes);
    }

    [Fact]
    public void Detect_WhenStateChanges_ReportsStateChange()
    {
        var previous = CreateStatus(state: "open");
        var current = CreateStatus(state: "merged");

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("State changed: open -> merged"));
    }

    [Fact]
    public void Detect_WhenChecksStateChanges_ReportsChecksChange()
    {
        var previous = CreateStatus(checksState: CheckState.Pending);
        var current = CreateStatus(checksState: CheckState.Failure);

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("Checks changed: Pending -> Failure"));
    }

    [Fact]
    public void Detect_WhenReviewStateChanges_ReportsReviewChange()
    {
        var previous = CreateStatus(reviewState: ReviewState.ReviewRequired);
        var current = CreateStatus(reviewState: ReviewState.Approved);

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("Review changed: ReviewRequired -> Approved"));
    }

    [Fact]
    public void Detect_WhenNewCommitPushed_ReportsNewCommit()
    {
        var previous = CreateStatus(sha: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var current = CreateStatus(sha: "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.StartsWith("New commit pushed: bbbbbbb"));
    }

    [Fact]
    public void Detect_WhenMergeConflictsAppear_ReportsMergeConflict()
    {
        var previous = CreateStatus(mergeable: true);
        var current = CreateStatus(mergeable: false);

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("Now has merge conflicts"));
    }

    [Fact]
    public void Detect_WhenMergeConflictsResolved_ReportsResolution()
    {
        var previous = CreateStatus(mergeable: false);
        var current = CreateStatus(mergeable: true);

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("No longer has merge conflicts"));
    }

    [Fact]
    public void Detect_ThrowsWhenCurrentIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ChangeDetector.Detect(CreateStatus(), null!));
    }

    [Fact]
    public void Detect_WhenPendingReviewersListChangesButReviewStateStaysTheSame_ReportsStillAwaiting()
    {
        var previous = CreateStatus(reviewState: ReviewState.ReviewRequired, pendingReviewers: new[] { "org/team-a", "org/team-b" });
        var current = CreateStatus(reviewState: ReviewState.ReviewRequired, pendingReviewers: new[] { "org/team-b" });

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Contains(changes, c => c.Contains("Still awaiting review from: org/team-b"));
    }

    [Fact]
    public void Detect_WhenPendingReviewersUnchanged_DoesNotReportAwaitingMessage()
    {
        var previous = CreateStatus(reviewState: ReviewState.ReviewRequired, pendingReviewers: new[] { "org/team-a" });
        var current = CreateStatus(reviewState: ReviewState.ReviewRequired, pendingReviewers: new[] { "org/team-a" });

        var changes = ChangeDetector.Detect(previous, current);

        Assert.Empty(changes);
    }
}
