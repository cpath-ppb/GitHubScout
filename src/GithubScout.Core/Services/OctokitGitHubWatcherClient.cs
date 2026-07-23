using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace GithubScout.Core.Services;

/// <summary>Fetches watch item status from the real GitHub API using Octokit.</summary>
public sealed class OctokitGitHubWatcherClient : IGitHubWatcherClient
{
    private readonly IGitHubClient _client;
    private readonly GitHubGraphQLReviewClient _graphQLReviewClient;
    private readonly ILogger<OctokitGitHubWatcherClient> _logger;

    public OctokitGitHubWatcherClient(IGitHubClient client, GitHubGraphQLReviewClient graphQLReviewClient, ILogger<OctokitGitHubWatcherClient> logger)
    {
        _client = client;
        _graphQLReviewClient = graphQLReviewClient;
        _logger = logger;
    }

    public async Task<WatchStatus> GetStatusAsync(WatchItem item, CancellationToken cancellationToken)
    {
        return item.Kind switch
        {
            WatchItemKind.PullRequest => await GetPullRequestStatusAsync(item, cancellationToken),
            WatchItemKind.Branch => await GetBranchStatusAsync(item, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.Kind, "Unsupported watch item kind")
        };
    }

    private async Task<WatchStatus> GetPullRequestStatusAsync(WatchItem item, CancellationToken cancellationToken)
    {
        var number = item.PullRequestNumber ?? throw new InvalidOperationException($"Watch item {item.Id} is missing a pull request number");
        var pr = await _client.PullRequest.Get(item.Owner, item.Repo, number);

        var checksState = await GetChecksStateAsync(item.Owner, item.Repo, pr.Head.Sha);
        var (reviewState, pendingReviewers) = await GetReviewStateAsync(item.Owner, item.Repo, number, cancellationToken);

        var state = pr.Merged ? "merged" : pr.Draft ? "draft" : pr.State.Value.ToString().ToLowerInvariant();

        return new WatchStatus
        {
            WatchItemId = item.Id,
            Title = pr.Title,
            AuthorLogin = pr.User?.Login,
            State = state,
            ChecksState = checksState,
            ReviewState = reviewState,
            LatestCommitSha = pr.Head.Sha,
            LatestCommitMessage = null,
            LatestCommitAtUtc = pr.UpdatedAt,
            Mergeable = pr.Mergeable,
            PendingReviewers = pendingReviewers,
            HtmlUrl = pr.HtmlUrl,
            CheckedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<WatchStatus> GetBranchStatusAsync(WatchItem item, CancellationToken cancellationToken)
    {
        var branchName = item.BranchName ?? throw new InvalidOperationException($"Watch item {item.Id} is missing a branch name");
        var branch = await _client.Repository.Branch.Get(item.Owner, item.Repo, branchName);
        var sha = branch.Commit.Sha;

        GitHubCommit? commit = null;
        try
        {
            commit = await _client.Repository.Commit.Get(item.Owner, item.Repo, sha);
        }
        catch (NotFoundException)
        {
            _logger.LogWarning("Commit {Sha} not found for {Item}", sha, item.DisplayName);
        }

        var checksState = await GetChecksStateAsync(item.Owner, item.Repo, sha);

        return new WatchStatus
        {
            WatchItemId = item.Id,
            Title = branchName,
            AuthorLogin = commit?.Author?.Login ?? commit?.Commit.Author.Name,
            State = "active",
            ChecksState = checksState,
            ReviewState = ReviewState.NoReviewRequired,
            LatestCommitSha = sha,
            LatestCommitMessage = commit?.Commit.Message,
            LatestCommitAtUtc = commit?.Commit.Author.Date,
            Mergeable = null,
            HtmlUrl = $"https://github.com/{item.Owner}/{item.Repo}/tree/{branchName}",
            CheckedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private async Task<CheckState> GetChecksStateAsync(string owner, string repo, string sha)
    {
        try
        {
            var checkRuns = await _client.Check.Run.GetAllForReference(owner, repo, sha);
            if (checkRuns.CheckRuns.Count > 0)
            {
                return AggregateCheckRuns(checkRuns.CheckRuns);
            }

            var combined = await _client.Repository.Status.GetCombined(owner, repo, sha);
            return combined.Statuses.Count == 0
                ? CheckState.NoChecks
                : combined.State.Value switch
                {
                    CommitState.Success => CheckState.Success,
                    CommitState.Pending => CheckState.Pending,
                    CommitState.Failure or CommitState.Error => CheckState.Failure,
                    _ => CheckState.Unknown
                };
        }
        catch (Exception ex) when (ex is ApiException)
        {
            _logger.LogWarning(ex, "Failed to read check status for {Owner}/{Repo}@{Sha}", owner, repo, sha);
            return CheckState.Unknown;
        }
    }

    private static CheckState AggregateCheckRuns(IReadOnlyList<CheckRun> checkRuns)
    {
        if (checkRuns.Any(r => r.Conclusion?.Value is CheckConclusion.Failure or CheckConclusion.TimedOut or CheckConclusion.Cancelled))
        {
            return CheckState.Failure;
        }

        if (checkRuns.Any(r => r.Status.Value is CheckStatus.Queued or CheckStatus.InProgress))
        {
            return CheckState.Pending;
        }

        return CheckState.Success;
    }

    private async Task<(ReviewState State, IReadOnlyList<string> PendingReviewers)> GetReviewStateAsync(string owner, string repo, int number, CancellationToken cancellationToken)
    {
        // GitHub's GraphQL `reviewDecision` is the authoritative signal used to decide whether
        // merging is actually blocked - it correctly reflects a still-pending CODEOWNERS team
        // review even in cases where the REST "requested reviewers" list has already been cleared.
        // Prefer it, and only fall back to the REST-based heuristic if GraphQL is unavailable.
        var graphQlInfo = await _graphQLReviewClient.GetReviewDecisionAsync(owner, repo, number, cancellationToken);
        var mappedState = GitHubGraphQLReviewClient.MapReviewDecision(graphQlInfo?.ReviewDecision);
        if (mappedState is not null)
        {
            var pendingReviewers = graphQlInfo!.PendingReviewers;
            if (mappedState.Value == ReviewState.ReviewRequired && pendingReviewers.Count == 0)
            {
                // GitHub cleared the explicit "requested reviewer" list (e.g. the CODEOWNERS team's
                // request was already consumed) but review is still required - fall back to
                // matching the PR's changed files against CODEOWNERS to name the responsible team(s).
                pendingReviewers = await GetCodeOwnersForPullRequestAsync(owner, repo, number, cancellationToken);
            }

            return (mappedState.Value, pendingReviewers);
        }

        try
        {
            var reviews = await _client.PullRequest.Review.GetAll(owner, repo, number);

            var latestPerReviewer = reviews
                .Where(r => r.User is not null)
                .GroupBy(r => r.User.Login)
                .Select(g => g.OrderByDescending(r => r.SubmittedAt).First())
                .Select(r => r.State.Value switch
                {
                    PullRequestReviewState.Approved => SubmittedReviewOutcome.Approved,
                    PullRequestReviewState.ChangesRequested => SubmittedReviewOutcome.ChangesRequested,
                    _ => SubmittedReviewOutcome.Other
                })
                .ToList();

            var pendingReviewers = await GetPendingRequestedReviewersAsync(owner, repo, number);

            var reviewState = ReviewStateCalculator.Calculate(latestPerReviewer, pendingReviewers.Count > 0);
            return (reviewState, pendingReviewers);
        }
        catch (Exception ex) when (ex is ApiException)
        {
            _logger.LogWarning(ex, "Failed to read reviews for {Owner}/{Repo}#{Number}", owner, repo, number);
            return (ReviewState.Unknown, Array.Empty<string>());
        }
    }

    private static readonly string[] CodeOwnersPaths =
    [
        ".github/CODEOWNERS",
        "CODEOWNERS",
        "docs/CODEOWNERS"
    ];

    private async Task<IReadOnlyList<string>> GetCodeOwnersForPullRequestAsync(string owner, string repo, int number, CancellationToken cancellationToken)
    {
        try
        {
            var codeOwnersContent = await GetCodeOwnersContentAsync(owner, repo);
            if (codeOwnersContent is null)
            {
                return Array.Empty<string>();
            }

            var entries = CodeOwnersParser.Parse(codeOwnersContent);
            if (entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            var files = await _client.PullRequest.Files(owner, repo, number);
            var changedPaths = files.Select(f => f.FileName).ToList();

            return CodeOwnersMatcher.MatchOwners(entries, changedPaths);
        }
        catch (Exception ex) when (ex is ApiException)
        {
            _logger.LogWarning(ex, "Failed to resolve CODEOWNERS for {Owner}/{Repo}#{Number}", owner, repo, number);
            return Array.Empty<string>();
        }
    }

    private async Task<string?> GetCodeOwnersContentAsync(string owner, string repo)
    {
        foreach (var path in CodeOwnersPaths)
        {
            try
            {
                var contents = await _client.Repository.Content.GetAllContents(owner, repo, path);
                var file = contents.FirstOrDefault();
                if (file?.Content is not null)
                {
                    return file.Content;
                }
            }
            catch (NotFoundException)
            {
                // Try the next well-known location.
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<string>> GetPendingRequestedReviewersAsync(string owner, string repo, int number)
    {
        try
        {
            var requests = await _client.PullRequest.ReviewRequest.Get(owner, repo, number);
            var names = requests.Users.Select(u => u.Login)
                .Concat(requests.Teams.Select(t => $"{owner}/{t.Slug}"))
                .ToList();
            return names;
        }
        catch (Exception ex) when (ex is ApiException)
        {
            _logger.LogWarning(ex, "Failed to read requested reviewers for {Owner}/{Repo}#{Number}", owner, repo, number);
            return Array.Empty<string>();
        }
    }
}
