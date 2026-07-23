using GithubScout.Core.Models;
using Xunit;

namespace GithubScout.Tests.Models;

public class WatchItemTests
{
    [Fact]
    public void BuildId_ForPullRequest_IsDeterministicAndLowercase()
    {
        var id1 = WatchItem.BuildId("Dotnet", "Runtime", WatchItemKind.PullRequest, null, 123);
        var id2 = WatchItem.BuildId("dotnet", "runtime", WatchItemKind.PullRequest, null, 123);

        Assert.Equal(id1, id2);
        Assert.Equal("dotnet/runtime/pr/123", id1);
    }

    [Fact]
    public void BuildId_ForBranch_IncludesBranchName()
    {
        var id = WatchItem.BuildId("dotnet", "runtime", WatchItemKind.Branch, "main", null);

        Assert.Equal("dotnet/runtime/branch/main", id);
    }

    [Fact]
    public void DisplayName_ForPullRequest_UsesAliasWhenSet()
    {
        var item = new WatchItem
        {
            Id = "id",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.PullRequest,
            PullRequestNumber = 42,
            Alias = "My PR"
        };

        Assert.Equal("My PR", item.DisplayName);
    }

    [Fact]
    public void DisplayName_ForPullRequest_WithoutAlias_GeneratesLabel()
    {
        var item = new WatchItem
        {
            Id = "id",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.PullRequest,
            PullRequestNumber = 42
        };

        Assert.Equal("dotnet/runtime#42", item.DisplayName);
    }

    [Fact]
    public void DisplayName_ForBranch_WithoutAlias_GeneratesLabel()
    {
        var item = new WatchItem
        {
            Id = "id",
            Owner = "dotnet",
            Repo = "runtime",
            Kind = WatchItemKind.Branch,
            BranchName = "main"
        };

        Assert.Equal("dotnet/runtime@main", item.DisplayName);
    }
}
