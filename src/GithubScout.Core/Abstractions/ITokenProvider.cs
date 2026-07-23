namespace GithubScout.Core.Abstractions;

/// <summary>Resolves the GitHub personal access token from whichever source is configured.</summary>
public interface ITokenProvider
{
    string? GetToken();
}
