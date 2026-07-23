using GithubScout.Core.Models;
using GithubScout.Core.Services;
using Xunit;

namespace GithubScout.Tests.Services;

public class ReviewStateCalculatorTests
{
    [Fact]
    public void Calculate_WhenAnyoneRequestedChanges_ReturnsChangesRequested()
    {
        var outcomes = new[] { SubmittedReviewOutcome.Approved, SubmittedReviewOutcome.ChangesRequested };

        var result = ReviewStateCalculator.Calculate(outcomes, hasPendingRequestedReviewers: false);

        Assert.Equal(ReviewState.ChangesRequested, result);
    }

    [Fact]
    public void Calculate_WhenApprovedButRequiredReviewerStillPending_ReturnsReviewRequired()
    {
        // This is the exact scenario reported: another team approved, but a required
        // CODEOWNERS team is still a pending requested reviewer - GitHub still blocks merging.
        var outcomes = new[] { SubmittedReviewOutcome.Approved };

        var result = ReviewStateCalculator.Calculate(outcomes, hasPendingRequestedReviewers: true);

        Assert.Equal(ReviewState.ReviewRequired, result);
    }

    [Fact]
    public void Calculate_WhenApprovedAndNoPendingReviewers_ReturnsApproved()
    {
        var outcomes = new[] { SubmittedReviewOutcome.Approved };

        var result = ReviewStateCalculator.Calculate(outcomes, hasPendingRequestedReviewers: false);

        Assert.Equal(ReviewState.Approved, result);
    }

    [Fact]
    public void Calculate_WhenNoReviewsAndNoPendingReviewers_ReturnsReviewRequired()
    {
        var result = ReviewStateCalculator.Calculate(Array.Empty<SubmittedReviewOutcome>(), hasPendingRequestedReviewers: false);

        Assert.Equal(ReviewState.ReviewRequired, result);
    }

    [Fact]
    public void Calculate_WhenNoReviewsButReviewersPending_ReturnsReviewRequired()
    {
        var result = ReviewStateCalculator.Calculate(Array.Empty<SubmittedReviewOutcome>(), hasPendingRequestedReviewers: true);

        Assert.Equal(ReviewState.ReviewRequired, result);
    }

    [Fact]
    public void Calculate_ChangesRequestedTakesPriorityOverPendingReviewers()
    {
        var outcomes = new[] { SubmittedReviewOutcome.ChangesRequested };

        var result = ReviewStateCalculator.Calculate(outcomes, hasPendingRequestedReviewers: true);

        Assert.Equal(ReviewState.ChangesRequested, result);
    }
}
