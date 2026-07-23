using GithubScout.Core.Abstractions;
using GithubScout.Core.Services;
using GithubScout.Web.Contracts;

namespace GithubScout.Web.Endpoints;

/// <summary>Minimal API endpoints for viewing status/activity and managing the watchlist live.</summary>
public static class WatchlistEndpoints
{
    public static IEndpointRouteBuilder MapWatchlistEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/status", async (IWatchlistStore store, IWatchStateRepository repository, CancellationToken ct) =>
        {
            var items = await store.GetAllAsync(ct);
            var result = items
                .Select(item => new
                {
                    item.Id,
                    item.Owner,
                    item.Repo,
                    Kind = item.Kind.ToString(),
                    item.BranchName,
                    item.PullRequestNumber,
                    DisplayName = item.DisplayName,
                    Status = repository.GetStatus(item.Id)
                })
                .OrderBy(x => x.Owner, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Repo, StringComparer.OrdinalIgnoreCase);

            return Results.Ok(result);
        });

        api.MapGet("/activity", (IWatchStateRepository repository, int? count) =>
            Results.Ok(repository.GetRecentActivity(count ?? 50)));

        api.MapPost("/watchlist", async (AddWatchItemRequest request, IWatchlistStore store, CancellationToken ct) =>
        {
            if (request.Kind == Core.Models.WatchItemKind.PullRequest && request.PullRequestNumber is null)
            {
                return Results.BadRequest("PullRequestNumber is required when Kind is PullRequest.");
            }

            if (request.Kind == Core.Models.WatchItemKind.Branch && string.IsNullOrWhiteSpace(request.BranchName))
            {
                return Results.BadRequest("BranchName is required when Kind is Branch.");
            }

            var item = await store.AddAsync(request.ToWatchItem(), ct);
            return Results.Ok(item);
        });

        api.MapPost("/watchlist/from-url", async (AddWatchItemFromUrlRequest request, IWatchlistStore store, CancellationToken ct) =>
        {
            if (!GitHubUrlParser.TryParse(request.Url, out var parsed) || parsed is null)
            {
                return Results.BadRequest("Could not recognize that as a GitHub pull request or branch URL (expected .../pull/123 or .../tree/branch-name).");
            }

            var item = parsed with { Alias = string.IsNullOrWhiteSpace(request.Alias) ? null : request.Alias };
            var added = await store.AddAsync(item, ct);
            return Results.Ok(added);
        });

        api.MapDelete("/watchlist/{*id}", async (string id, IWatchlistStore store, IWatchStateRepository repository, CancellationToken ct) =>
        {
            var removed = await store.RemoveAsync(id, ct);
            if (removed)
            {
                repository.RemoveItem(id);
            }

            return removed ? Results.Ok() : Results.NotFound();
        });

        return app;
    }
}
