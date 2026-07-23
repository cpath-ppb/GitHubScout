using System.Net.Http.Headers;
using GithubScout.Core.Abstractions;

namespace GithubScout.Core.Services;

/// <summary>Attaches the configured GitHub token as a bearer <c>Authorization</c> header on every request.</summary>
public sealed class GitHubAuthHeaderHandler : DelegatingHandler
{
    private readonly ITokenProvider _tokenProvider;

    public GitHubAuthHeaderHandler(ITokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = _tokenProvider.GetToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
