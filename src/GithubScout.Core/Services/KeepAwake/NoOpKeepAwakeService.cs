using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Used when keep-awake is disabled or unsupported (e.g. inside a container).</summary>
public sealed class NoOpKeepAwakeService : IKeepAwakeService
{
    private readonly ILogger<NoOpKeepAwakeService> _logger;
    private readonly string _reason;

    public NoOpKeepAwakeService(ILogger<NoOpKeepAwakeService> logger, string reason)
    {
        _logger = logger;
        _reason = reason;
    }

    public bool IsSupported => false;

    public string PlatformDescription => $"None ({_reason})";

    public void Start() => _logger.LogInformation("Keep-awake is not active: {Reason}", _reason);

    public void Stop()
    {
    }
}
