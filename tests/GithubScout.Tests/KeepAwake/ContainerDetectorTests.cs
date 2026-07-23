using GithubScout.Core.Services.KeepAwake;
using Xunit;

namespace GithubScout.Tests.KeepAwake;

public class ContainerDetectorTests
{
    [Fact]
    public void IsRunningInContainer_WhenDotnetFlagSet_ReturnsTrue()
    {
        var env = new Dictionary<string, string?> { ["DOTNET_RUNNING_IN_CONTAINER"] = "true" };

        var result = ContainerDetector.IsRunningInContainer(env, _ => false);

        Assert.True(result);
    }

    [Fact]
    public void IsRunningInContainer_WhenCustomFlagSet_ReturnsTrue()
    {
        var env = new Dictionary<string, string?> { ["GITHUBSCOUT_IN_CONTAINER"] = "true" };

        var result = ContainerDetector.IsRunningInContainer(env, _ => false);

        Assert.True(result);
    }

    [Fact]
    public void IsRunningInContainer_WhenDockerEnvFileExists_ReturnsTrue()
    {
        var env = new Dictionary<string, string?>();

        var result = ContainerDetector.IsRunningInContainer(env, path => path == "/.dockerenv");

        Assert.True(result);
    }

    [Fact]
    public void IsRunningInContainer_WhenNoIndicatorsPresent_ReturnsFalse()
    {
        var env = new Dictionary<string, string?>();

        var result = ContainerDetector.IsRunningInContainer(env, _ => false);

        Assert.False(result);
    }
}
