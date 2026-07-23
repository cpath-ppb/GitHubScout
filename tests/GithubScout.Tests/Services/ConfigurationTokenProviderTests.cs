using GithubScout.Core.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GithubScout.Tests.Services;

[Collection("EnvironmentVariable")]
public class ConfigurationTokenProviderTests
{
    private const string EnvVarName = "GITHUB_TOKEN";

    [Fact]
    public void GetToken_WhenConfigurationHasValue_ReturnsConfigurationValue()
    {
        var previousEnv = Environment.GetEnvironmentVariable(EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, null);
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["GithubScout:GitHubToken"] = "config-token" })
                .Build();
            var sut = new ConfigurationTokenProvider(configuration);

            Assert.Equal("config-token", sut.GetToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, previousEnv);
        }
    }

    [Fact]
    public void GetToken_WhenConfigurationMissing_FallsBackToEnvironmentVariable()
    {
        var previousEnv = Environment.GetEnvironmentVariable(EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, "env-token");
            var configuration = new ConfigurationBuilder().Build();
            var sut = new ConfigurationTokenProvider(configuration);

            Assert.Equal("env-token", sut.GetToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, previousEnv);
        }
    }

    [Fact]
    public void GetToken_WhenNeitherSourceHasValue_ReturnsNull()
    {
        var previousEnv = Environment.GetEnvironmentVariable(EnvVarName);
        try
        {
            Environment.SetEnvironmentVariable(EnvVarName, null);
            var configuration = new ConfigurationBuilder().Build();
            var sut = new ConfigurationTokenProvider(configuration);

            Assert.Null(sut.GetToken());
        }
        finally
        {
            Environment.SetEnvironmentVariable(EnvVarName, previousEnv);
        }
    }
}
