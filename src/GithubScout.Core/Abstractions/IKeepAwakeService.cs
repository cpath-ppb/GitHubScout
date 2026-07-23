namespace GithubScout.Core.Abstractions;

/// <summary>
/// Prevents the machine from sleeping / losing network connectivity while GithubScout is running.
/// Implementations are OS-specific; a no-op implementation is used when unsupported
/// (e.g. running inside a container, which has no access to the host's power state).
/// </summary>
public interface IKeepAwakeService
{
    /// <summary>Whether this implementation can actually keep the machine awake on the current host.</summary>
    bool IsSupported { get; }

    /// <summary>Human-readable description of the mechanism in use, for logging/diagnostics.</summary>
    string PlatformDescription { get; }

    void Start();

    void Stop();
}
