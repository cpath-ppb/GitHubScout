using System.Diagnostics;
using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>
/// Base class for keep-awake implementations that work by launching and holding open a
/// long-running helper process (e.g. <c>caffeinate</c> on macOS, <c>systemd-inhibit</c> on Linux).
/// </summary>
public abstract class ProcessBasedKeepAwakeService : IKeepAwakeService
{
    private readonly ILogger _logger;
    private Process? _process;
    private readonly object _sync = new();

    protected ProcessBasedKeepAwakeService(ILogger logger)
    {
        _logger = logger;
    }

    public abstract bool IsSupported { get; }

    public abstract string PlatformDescription { get; }

    protected abstract ProcessStartInfo BuildProcessStartInfo();

    public void Start()
    {
        if (!IsSupported)
        {
            return;
        }

        lock (_sync)
        {
            if (_process is { HasExited: false })
            {
                return;
            }

            try
            {
                _process = Process.Start(BuildProcessStartInfo());
                _logger.LogInformation("Keep-awake enabled via {Description}", PlatformDescription);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start keep-awake helper process for {Description}", PlatformDescription);
                _process = null;
            }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_process is null)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to stop keep-awake helper process for {Description}", PlatformDescription);
            }
            finally
            {
                _process.Dispose();
                _process = null;
                _logger.LogInformation("Keep-awake disabled");
            }
        }
    }
}
