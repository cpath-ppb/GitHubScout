using System.Runtime.InteropServices;
using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Keeps Windows from sleeping/turning off the display via SetThreadExecutionState.</summary>
public sealed class WindowsKeepAwakeService : IKeepAwakeService
{
    [Flags]
    private enum ExecutionState : uint
    {
        EsContinuous = 0x80000000,
        EsSystemRequired = 0x00000001,
        EsAwaymodeRequired = 0x00000040
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    private readonly ILogger<WindowsKeepAwakeService> _logger;

    public WindowsKeepAwakeService(ILogger<WindowsKeepAwakeService> logger)
    {
        _logger = logger;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public string PlatformDescription => "Windows (SetThreadExecutionState)";

    public void Start()
    {
        if (!IsSupported)
        {
            return;
        }

        var result = SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
        if (result == 0)
        {
            _logger.LogWarning("SetThreadExecutionState failed to keep the system awake");
        }
        else
        {
            _logger.LogInformation("Keep-awake enabled via SetThreadExecutionState");
        }
    }

    public void Stop()
    {
        if (!IsSupported)
        {
            return;
        }

        SetThreadExecutionState(ExecutionState.EsContinuous);
        _logger.LogInformation("Keep-awake disabled");
    }
}
