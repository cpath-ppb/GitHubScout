namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Environment detection helpers, split out so they're trivially unit testable.</summary>
public static class ContainerDetector
{
    /// <summary>
    /// Detects whether the process is running inside a container. Containers have no access to
    /// the host machine's power/sleep state, so keep-awake must no-op in that case.
    /// </summary>
    public static bool IsRunningInContainer(IDictionary<string, string?> environmentVariables, Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(environmentVariables);
        ArgumentNullException.ThrowIfNull(fileExists);

        if (environmentVariables.TryGetValue("DOTNET_RUNNING_IN_CONTAINER", out var dotnetFlag)
            && string.Equals(dotnetFlag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (environmentVariables.TryGetValue("GITHUBSCOUT_IN_CONTAINER", out var customFlag)
            && string.Equals(customFlag, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return fileExists("/.dockerenv");
    }

    public static bool IsRunningInContainer() => IsRunningInContainer(
        Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value),
        File.Exists);
}
