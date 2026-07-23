using GithubScout.Core.Models;
using GithubScout.Core.Services;
using Xunit;

namespace GithubScout.Tests.Services;

public class GitHubUrlParserTests
{
    [Theory]
    [InlineData("https://github.com/dotnet/runtime/pull/12345")]
    [InlineData("https://github.com/dotnet/runtime/pull/12345/")]
    [InlineData("https://github.com/dotnet/runtime/pull/12345/files")]
    [InlineData("http://github.com/dotnet/runtime/pull/12345")]
    public void TryParse_PullRequestUrl_ParsesOwnerRepoAndNumber(string url)
    {
        var result = GitHubUrlParser.TryParse(url, out var item);

        Assert.True(result);
        Assert.NotNull(item);
        Assert.Equal("dotnet", item!.Owner);
        Assert.Equal("runtime", item.Repo);
        Assert.Equal(WatchItemKind.PullRequest, item.Kind);
        Assert.Equal(12345, item.PullRequestNumber);
        Assert.Equal("dotnet/runtime/pr/12345", item.Id);
    }

    [Theory]
    [InlineData("https://github.com/dotnet/runtime/tree/main", "main")]
    [InlineData("https://github.com/dotnet/runtime/tree/feature/my-branch", "feature/my-branch")]
    public void TryParse_BranchUrl_ParsesOwnerRepoAndBranch(string url, string expectedBranch)
    {
        var result = GitHubUrlParser.TryParse(url, out var item);

        Assert.True(result);
        Assert.NotNull(item);
        Assert.Equal("dotnet", item!.Owner);
        Assert.Equal("runtime", item.Repo);
        Assert.Equal(WatchItemKind.Branch, item.Kind);
        Assert.Equal(expectedBranch, item.BranchName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("https://gitlab.com/dotnet/runtime/pull/1")]
    [InlineData("https://github.com/dotnet/runtime")]
    [InlineData("https://github.com/dotnet/runtime/issues/1")]
    public void TryParse_WhenUrlIsInvalidOrUnsupported_ReturnsFalse(string? url)
    {
        var result = GitHubUrlParser.TryParse(url, out var item);

        Assert.False(result);
        Assert.Null(item);
    }
}
