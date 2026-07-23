using GithubScout.Core.Abstractions;
using GithubScout.Core.Models;
using Microsoft.Extensions.Hosting;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace GithubScout.Web.ConsoleDashboard;

/// <summary>
/// Renders a continuously refreshing Spectre.Console dashboard in the terminal, showing every
/// watched item's current status alongside a rolling activity feed.
/// </summary>
public sealed class ConsoleDashboardService : BackgroundService
{
    private readonly IWatchlistStore _watchlistStore;
    private readonly IWatchStateRepository _stateRepository;

    public ConsoleDashboardService(IWatchlistStore watchlistStore, IWatchStateRepository stateRepository)
    {
        _watchlistStore = watchlistStore;
        _stateRepository = stateRepository;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (Console.IsOutputRedirected)
        {
            // No interactive terminal (e.g. running under a container/CI log) - skip the live dashboard.
            return;
        }

        await AnsiConsole.Live(BuildLayout())
            .StartAsync(async ctx =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    ctx.UpdateTarget(BuildLayout());
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            });
    }

    private IRenderable BuildLayout()
    {
        var items = _watchlistStore.GetAllAsync().GetAwaiter().GetResult();

        var statusTable = new Table().Expand().Title("[bold yellow]GithubScout - Watched Items[/]");
        statusTable.AddColumn("Item");
        statusTable.AddColumn("State");
        statusTable.AddColumn("Checks");
        statusTable.AddColumn("Review");
        statusTable.AddColumn("Latest Commit");
        statusTable.AddColumn("Checked At");

        foreach (var item in items)
        {
            var status = _stateRepository.GetStatus(item.Id);
            statusTable.AddRow(
                item.DisplayName,
                status is null ? "[grey]pending[/]" : Markup.Escape(status.State),
                FormatChecks(status?.ChecksState),
                FormatReview(status?.ReviewState, status?.PendingReviewers),
                status?.LatestCommitSha is null ? "-" : Markup.Escape(status.LatestCommitSha[..Math.Min(7, status.LatestCommitSha.Length)]),
                status is null ? "-" : status.CheckedAtUtc.ToLocalTime().ToString("HH:mm:ss"));
        }

        var activityTable = new Table().Expand().Title("[bold yellow]Recent Activity[/]");
        activityTable.AddColumn("Time");
        activityTable.AddColumn("Item");
        activityTable.AddColumn("Change");

        foreach (var activity in _stateRepository.GetRecentActivity(15))
        {
            activityTable.AddRow(
                activity.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"),
                Markup.Escape(activity.WatchItemDisplayName),
                Markup.Escape(activity.Message));
        }

        var rows = new Rows(statusTable, activityTable);
        return new Panel(rows)
            .Header("[bold cyan] GithubScout [/]")
            .Expand();
    }

    private static string FormatChecks(CheckState? state) => state switch
    {
        CheckState.Success => "[green]passing[/]",
        CheckState.Failure => "[red]failing[/]",
        CheckState.Pending => "[yellow]pending[/]",
        CheckState.NoChecks => "[grey]none[/]",
        _ => "[grey]unknown[/]"
    };

    private static string FormatReview(ReviewState? state, IReadOnlyList<string>? pendingReviewers)
    {
        var badge = state switch
        {
            ReviewState.Approved => "[green]approved[/]",
            ReviewState.ChangesRequested => "[red]changes requested[/]",
            ReviewState.ReviewRequired => "[yellow]awaiting review[/]",
            ReviewState.NoReviewRequired => "[grey]n/a[/]",
            _ => "[grey]unknown[/]"
        };

        if (state == ReviewState.ReviewRequired && pendingReviewers is { Count: > 0 })
        {
            badge += $" [grey]from {Markup.Escape(string.Join(", ", pendingReviewers))}[/]";
        }

        return badge;
    }
}
