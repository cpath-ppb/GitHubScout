namespace GithubScout.Web.Contracts;

/// <summary>Request body for adding a watchlist entry from a pasted GitHub URL.</summary>
public sealed class AddWatchItemFromUrlRequest
{
    public required string Url { get; init; }

    public string? Alias { get; init; }
}
