using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GithubScout.Core.Services.KeepAwake;

/// <summary>Keeps Linux from sleeping/idling by holding open a <c>systemd-inhibit</c> lock.</summary>
public sealed class LinuxKeepAwakeService : ProcessBasedKeepAwakeService
{
    public LinuxKeepAwakeService(ILogger<LinuxKeepAwakeService> logger) : base(logger)
    {
    }

    public override bool IsSupported => OperatingSystem.IsLinux();

    public override string PlatformDescription => "Linux (systemd-inhibit)";

    protected override ProcessStartInfo BuildProcessStartInfo() => new()
    {
        FileName = "systemd-inhibit",
        Arguments = "--what=sleep:idle --who=GithubScout --why=\"Watching pull requests\" --mode=block sleep infinity",
        UseShellExecute = false,
        CreateNoWindow = true
    };
}
