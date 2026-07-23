namespace GithubScout.Core.Configuration;

/// <summary>Strongly typed binding for the "GithubScout" configuration section.</summary>
public sealed class GithubScoutOptions
{
    public const string SectionName = "GithubScout";

    /// <summary>How often to poll GitHub for every watched item.</summary>
    public int PollIntervalSeconds { get; set; } = 120;

    /// <summary>Path to the JSON file holding the watchlist. Relative paths are resolved against the content root.</summary>
    public string WatchlistFilePath { get; set; } = "watchlist.json";

    /// <summary>Whether to attempt Windows toast notifications (no-op automatically on other OSes).</summary>
    public bool EnableToastNotifications { get; set; } = true;

    /// <summary>Whether to attempt to keep the machine awake while GithubScout runs.</summary>
    public bool EnableKeepAwake { get; set; } = true;

    /// <summary>How many activity feed entries to retain in memory.</summary>
    public int MaxActivityEntries { get; set; } = 200;
}
