using GithubScout.Core.Models;

namespace GithubScout.Web.Contracts;

/// <summary>Request body for adding a new entry to the watchlist.</summary>
public sealed class AddWatchItemRequest
{
    public required string Owner { get; init; }

    public required string Repo { get; init; }

    public required WatchItemKind Kind { get; init; }

    public string? BranchName { get; init; }

    public int? PullRequestNumber { get; init; }

    public string? Alias { get; init; }

    public WatchItem ToWatchItem()
    {
        var id = WatchItem.BuildId(Owner, Repo, Kind, BranchName, PullRequestNumber);
        return new WatchItem
        {
            Id = id,
            Owner = Owner,
            Repo = Repo,
            Kind = Kind,
            BranchName = BranchName,
            PullRequestNumber = PullRequestNumber,
            Alias = Alias
        };
    }
}
