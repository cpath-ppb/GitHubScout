using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Keeps macOS from sleeping by holding open a <c>caffeinate</c> process.</summary>
public sealed class MacKeepAwakeService : ProcessBasedKeepAwakeService
{
    public MacKeepAwakeService(ILogger<MacKeepAwakeService> logger) : base(logger)
    {
    }

    public override bool IsSupported => OperatingSystem.IsMacOS();

    public override string PlatformDescription => "macOS (caffeinate)";

    protected override ProcessStartInfo BuildProcessStartInfo() => new()
    {
        FileName = "caffeinate",
        Arguments = "-d -i -m -s",
        UseShellExecute = false,
        CreateNoWindow = true
    };
}
