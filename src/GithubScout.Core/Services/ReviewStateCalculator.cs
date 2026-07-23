using GithubScout.Core.Models;

namespace GithubScout.Core.Services;

/// <summary>The outcome of the most recent review submitted by a single reviewer.</summary>
public enum SubmittedReviewOutcome
{
    Approved,
    ChangesRequested,
    Other
}

/// <summary>
/// Pure, side-effect-free logic for deciding a PR's overall <see cref="ReviewState"/>.
/// Crucially, an approval from one reviewer does NOT count as "Approved" while a
/// required reviewer/team (e.g. a CODEOWNERS team) is still pending - GitHub itself
/// will keep merging blocked in that case, so GithubScout must not report "Approved" either.
/// </summary>
public static class ReviewStateCalculator
{
    public static ReviewState Calculate(IReadOnlyList<SubmittedReviewOutcome> latestPerReviewer, bool hasPendingRequestedReviewers)
    {
        ArgumentNullException.ThrowIfNull(latestPerReviewer);

        if (latestPerReviewer.Any(r => r == SubmittedReviewOutcome.ChangesRequested))
        {
            return ReviewState.ChangesRequested;
        }

        if (hasPendingRequestedReviewers)
        {
            // Someone may have approved already, but a required/requested reviewer or team
            // (commonly a CODEOWNERS team) hasn't weighed in yet, so the PR is still blocked.
            return ReviewState.ReviewRequired;
        }

        if (latestPerReviewer.Any(r => r == SubmittedReviewOutcome.Approved))
        {
            return ReviewState.Approved;
        }

        return ReviewState.ReviewRequired;
    }
}
