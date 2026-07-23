namespace GithubScout.Core.Models;

public enum ActivitySeverity
{
    Info,
    Warning,
    Error
}

/// <summary>An entry in the rolling activity feed shown on the console dashboard and the website.</summary>
public sealed record ActivityEvent
{
    public required DateTimeOffset TimestampUtc { get; init; }

    public required string WatchItemId { get; init; }

    public required string WatchItemDisplayName { get; init; }

    public required string Message { get; init; }

    public ActivitySeverity Severity { get; init; } = ActivitySeverity.Info;
}
