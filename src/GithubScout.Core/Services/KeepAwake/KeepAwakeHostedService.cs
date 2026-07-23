using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Starts the configured <see cref="IKeepAwakeService"/> on app start and stops it on shutdown.</summary>
public sealed class KeepAwakeHostedService : IHostedService
{
    private readonly IKeepAwakeService _keepAwakeService;
    private readonly ILogger<KeepAwakeHostedService> _logger;

    public KeepAwakeHostedService(IKeepAwakeService keepAwakeService, ILogger<KeepAwakeHostedService> logger)
    {
        _keepAwakeService = keepAwakeService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Keep-awake mechanism: {Description}", _keepAwakeService.PlatformDescription);
        _keepAwakeService.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _keepAwakeService.Stop();
        return Task.CompletedTask;
    }
}
