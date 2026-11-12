using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Selects the correct <see cref="IKeepAwakeService"/> implementation for the current host.</summary>
public sealed class KeepAwakeServiceFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly KeepAwakeDiagnostics _diagnostics;

    public KeepAwakeServiceFactory(ILoggerFactory loggerFactory, KeepAwakeDiagnostics diagnostics)
    {
        _loggerFactory = loggerFactory;
        _diagnostics = diagnostics;
    }

    public IKeepAwakeService Create(bool enabled)
    {
        if (!enabled)
        {
            return new NoOpKeepAwakeService(_loggerFactory.CreateLogger<NoOpKeepAwakeService>(), "disabled via configuration");
        }

        if (ContainerDetector.IsRunningInContainer())
        {
            return new NoOpKeepAwakeService(_loggerFactory.CreateLogger<NoOpKeepAwakeService>(), "running inside a container, no access to host power state");
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsKeepAwakeService(_loggerFactory.CreateLogger<WindowsKeepAwakeService>(), _diagnostics);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxKeepAwakeService(_loggerFactory.CreateLogger<LinuxKeepAwakeService>());
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacKeepAwakeService(_loggerFactory.CreateLogger<MacKeepAwakeService>());
        }

        return new NoOpKeepAwakeService(_loggerFactory.CreateLogger<NoOpKeepAwakeService>(), "unsupported operating system");
    }
}
