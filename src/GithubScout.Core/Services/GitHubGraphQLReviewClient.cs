using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GithubScout.Core.Models;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services;

/// <summary>
/// Raw GraphQL response shape for the pull request review query.
/// </summary>
public sealed record GraphQLReviewInfo(string? ReviewDecision, IReadOnlyList<string> PendingReviewers);

/// <summary>
/// Calls GitHub's GraphQL API to read the authoritative <c>reviewDecision</c> for a pull request.
/// Unlike the REST "requested reviewers" endpoint, <c>reviewDecision</c> is what GitHub itself
/// uses to decide whether merging is blocked (e.g. it correctly accounts for a still-pending
/// CODEOWNERS team review even after that team no longer shows up as a "requested reviewer").
/// </summary>
public class GitHubGraphQLReviewClient
{
    private const string Query = """
        query($owner: String!, $repo: String!, $number: Int!) {
          repository(owner: $owner, name: $repo) {
            pullRequest(number: $number) {
              reviewDecision
              reviewRequests(first: 20) {
                nodes {
                  requestedReviewer {
                    __typename
                    ... on User { login }
                    ... on Team { organization { login } slug }
                  }
                }
              }
            }
          }
        }
        """;

    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubGraphQLReviewClient> _logger;

    public GitHubGraphQLReviewClient(HttpClient httpClient, ILogger<GitHubGraphQLReviewClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public virtual async Task<GraphQLReviewInfo?> GetReviewDecisionAsync(string owner, string repo, int number, CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                query = Query,
                variables = new { owner, repo, number }
            };

            using var response = await _httpClient.PostAsJsonAsync("graphql", payload, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<GraphQLEnvelope>(cancellationToken: cancellationToken);
            var pr = body?.Data?.Repository?.PullRequest;
            if (pr is null)
            {
                return null;
            }

            var pendingReviewers = (pr.ReviewRequests?.Nodes ?? [])
                .Select(n => n.RequestedReviewer)
                .Where(r => r is not null)
                .Select(r => r!.Typename == "Team"
                    ? $"{r.Organization?.Login}/{r.Slug}"
                    : r.Login)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();

            return new GraphQLReviewInfo(pr.ReviewDecision, pendingReviewers);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Failed to read GraphQL review decision for {Owner}/{Repo}#{Number}", owner, repo, number);
            return null;
        }
    }

    /// <summary>Maps GitHub's GraphQL <c>PullRequestReviewDecision</c> enum to our own <see cref="ReviewState"/>.</summary>
    public static ReviewState? MapReviewDecision(string? reviewDecision) => reviewDecision switch
    {
        "APPROVED" => ReviewState.Approved,
        "CHANGES_REQUESTED" => ReviewState.ChangesRequested,
        "REVIEW_REQUIRED" => ReviewState.ReviewRequired,
        _ => null
    };

    private sealed class GraphQLEnvelope
    {
        [JsonPropertyName("data")]
        public GraphQLData? Data { get; init; }
    }

    private sealed class GraphQLData
    {
        [JsonPropertyName("repository")]
        public GraphQLRepository? Repository { get; init; }
    }

    private sealed class GraphQLRepository
    {
        [JsonPropertyName("pullRequest")]
        public GraphQLPullRequest? PullRequest { get; init; }
    }

    private sealed class GraphQLPullRequest
    {
        [JsonPropertyName("reviewDecision")]
        public string? ReviewDecision { get; init; }

        [JsonPropertyName("reviewRequests")]
        public GraphQLReviewRequests? ReviewRequests { get; init; }
    }

    private sealed class GraphQLReviewRequests
    {
        [JsonPropertyName("nodes")]
        public List<GraphQLReviewRequestNode>? Nodes { get; init; }
    }

    private sealed class GraphQLReviewRequestNode
    {
        [JsonPropertyName("requestedReviewer")]
        public GraphQLRequestedReviewer? RequestedReviewer { get; init; }
    }

    private sealed class GraphQLRequestedReviewer
    {
        [JsonPropertyName("__typename")]
        public string? Typename { get; init; }

        [JsonPropertyName("login")]
        public string? Login { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("organization")]
        public GraphQLOrganization? Organization { get; init; }
    }

    private sealed class GraphQLOrganization
    {
        [JsonPropertyName("login")]
        public string? Login { get; init; }
    }
}
