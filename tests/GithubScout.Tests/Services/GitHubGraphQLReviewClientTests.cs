using GithubScout.Core.Models;
using GithubScout.Core.Services;
using Xunit;

namespace GithubScout.Tests.Services;

public class GitHubGraphQLReviewClientTests
{
    [Theory]
    [InlineData("APPROVED", ReviewState.Approved)]
    [InlineData("CHANGES_REQUESTED", ReviewState.ChangesRequested)]
    [InlineData("REVIEW_REQUIRED", ReviewState.ReviewRequired)]
    public void MapReviewDecision_KnownValues_MapsToExpectedReviewState(string decision, ReviewState expected)
    {
        var result = GitHubGraphQLReviewClient.MapReviewDecision(decision);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SOMETHING_UNEXPECTED")]
    public void MapReviewDecision_NullOrUnknown_ReturnsNull(string? decision)
    {
        // A null reviewDecision means the repo has no required-review branch protection at all;
        // callers should fall back to the REST-based heuristic in that case.
        var result = GitHubGraphQLReviewClient.MapReviewDecision(decision);

        Assert.Null(result);
    }

    [Fact]
    public void MapReviewDecision_ReviewRequiredEvenWithoutPendingReviewers_StillReturnsReviewRequired()
    {
        // Regression test for the exact bug reported: GitHub's REST "requested reviewers" list can
        // be empty (the CODEOWNERS team was already "used up") while GraphQL still reports
        // REVIEW_REQUIRED because the required review was never actually satisfied by that team.
        var info = new GraphQLReviewInfo("REVIEW_REQUIRED", Array.Empty<string>());

        var result = GitHubGraphQLReviewClient.MapReviewDecision(info.ReviewDecision);

        Assert.Equal(ReviewState.ReviewRequired, result);
    }
}
