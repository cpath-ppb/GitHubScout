using GithubScout.Core.Abstractions;
using Microsoft.Extensions.Configuration;

namespace GithubScout.Core.Services;

/// <summary>
/// Resolves the GitHub token from .NET user-secrets / appsettings configuration first
/// (key "GithubScout:GitHubToken"), falling back to the GITHUB_TOKEN environment variable.
/// Both mechanisms are cross-platform.
/// </summary>
public sealed class ConfigurationTokenProvider : ITokenProvider
{
    private readonly IConfiguration _configuration;

    public ConfigurationTokenProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? GetToken()
    {
        var fromConfig = _configuration["GithubScout:GitHubToken"];
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        var fromEnv = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(fromEnv) ? null : fromEnv;
    }
}
