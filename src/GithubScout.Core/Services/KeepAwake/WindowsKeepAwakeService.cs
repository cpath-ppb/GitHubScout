using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>
/// Keeps Windows from sleeping via SetThreadExecutionState, and logs keep-awake and network state
/// changes to both the logger and <see cref="KeepAwakeDiagnostics"/>.
/// </summary>
/// <remarks>
/// SetThreadExecutionState is per-thread: the requirement only lasts while the calling thread is alive.
/// So the flag is held by a dedicated background thread that re-asserts it periodically, instead of
/// relying on whichever thread happened to run startup.
/// </remarks>
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

    private static readonly TimeSpan ReassertInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(10);

    private readonly ILogger<WindowsKeepAwakeService> _logger;
    private readonly KeepAwakeDiagnostics _diagnostics;
    private readonly object _sync = new();
    private Thread? _worker;
    private CancellationTokenSource? _cts;

    public WindowsKeepAwakeService(ILogger<WindowsKeepAwakeService> logger, KeepAwakeDiagnostics diagnostics)
    {
        _logger = logger;
        _diagnostics = diagnostics;
    }

    public bool IsSupported => OperatingSystem.IsWindows();

    public string PlatformDescription => "Windows (SetThreadExecutionState, dedicated thread)";

    public void Start()
    {
        if (!IsSupported)
        {
            return;
        }

        lock (_sync)
        {
            if (_worker is not null)
            {
                return;
            }

            var cts = new CancellationTokenSource();
            var worker = new Thread(() => Run(cts.Token))
            {
                IsBackground = true,
                Name = "GithubScout.KeepAwake"
            };
            _cts = cts;
            _worker = worker;
            worker.Start();
        }

        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        Log(LogLevel.Information, $"Keep-awake started. Diagnostics file: {_diagnostics.LogFilePath}");
    }

    public void Stop()
    {
        if (!IsSupported)
        {
            return;
        }

        Thread? worker;
        CancellationTokenSource? cts;
        lock (_sync)
        {
            worker = _worker;
            cts = _cts;
            _worker = null;
            _cts = null;
        }

        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;

        if (cts is not null)
        {
            cts.Cancel();
        }

        // The worker clears its own execution state on exit, because the state belongs to that thread.
        worker?.Join(TimeSpan.FromSeconds(5));
        cts?.Dispose();

        Log(LogLevel.Information, "Keep-awake stopped");
    }

    private void Run(CancellationToken token)
    {
        Log(LogLevel.Information, $"Keep-awake worker thread started (managed thread {Environment.CurrentManagedThreadId}), re-asserting every {ReassertInterval.TotalSeconds}s");

        uint? lastPrevious = null;
        var lastNetworkUp = IsAnyNetworkUp();
        var lastHeartbeat = DateTimeOffset.UtcNow;

        while (!token.IsCancellationRequested)
        {
            var previous = SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);

            if (previous == 0)
            {
                // SetThreadExecutionState returns the previous state, so 0 means the call failed.
                var error = Marshal.GetLastPInvokeError();
                Log(LogLevel.Warning, $"SetThreadExecutionState FAILED (Win32 error {error}); system may sleep");
            }
            else if (lastPrevious is null || lastPrevious.Value != (uint)previous)
            {
                Log(LogLevel.Information, $"Execution state asserted OK (previous state 0x{(uint)previous:X8})");
                lastPrevious = (uint)previous;
            }

            var networkUp = IsAnyNetworkUp();
            if (networkUp != lastNetworkUp)
            {
                Log(LogLevel.Warning, $"Network state changed: {(networkUp ? "UP" : "DOWN")}. Adapters: {DescribeUpAdapters()}");
                lastNetworkUp = networkUp;
            }

            if (DateTimeOffset.UtcNow - lastHeartbeat >= HeartbeatInterval)
            {
                Log(LogLevel.Information, $"Heartbeat: keep-awake held, network {(networkUp ? "UP" : "DOWN")}, adapters: {DescribeUpAdapters()}");
                lastHeartbeat = DateTimeOffset.UtcNow;
            }

            token.WaitHandle.WaitOne(ReassertInterval);
        }

        var cleared = SetThreadExecutionState(ExecutionState.EsContinuous);
        Log(LogLevel.Information, cleared == 0
            ? $"Worker exiting; clearing execution state FAILED (Win32 error {Marshal.GetLastPInvokeError()})"
            : "Worker exiting; execution state cleared");
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        Log(LogLevel.Warning, $"NetworkAvailabilityChanged event: available={e.IsAvailable}. Adapters: {DescribeUpAdapters()}");
    }

    private static bool IsAnyNetworkUp()
    {
        return NetworkInterface.GetIsNetworkAvailable();
    }

    private static string DescribeUpAdapters()
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .Select(n => $"{n.Name} ({n.NetworkInterfaceType}, {n.Speed / 1_000_000} Mbps)")
                .ToArray();

            return adapters.Length == 0 ? "none up" : string.Join("; ", adapters);
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.GetType().Name})";
        }
    }

    private void Log(LogLevel level, string message)
    {
        _logger.Log(level, "{Message}", message);
        _diagnostics.Write($"[{level}] {message}");
    }
}
