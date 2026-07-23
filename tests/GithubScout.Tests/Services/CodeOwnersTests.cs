using GithubScout.Core.Services;
using Xunit;

namespace GithubScout.Tests.Services;

public class CodeOwnersParserTests
{
    [Fact]
    public void Parse_IgnoresBlankLinesAndComments()
    {
        var content = """
            # This is a comment

            * @Flutter-Global/global-owners
            """;

        var entries = CodeOwnersParser.Parse(content);

        Assert.Single(entries);
        Assert.Equal("*", entries[0].Pattern);
        Assert.Equal(["Flutter-Global/global-owners"], entries[0].Owners);
    }

    [Fact]
    public void Parse_StripsLeadingAtFromOwners()
    {
        var entries = CodeOwnersParser.Parse("/src/payments/ @Flutter-Global/maintainers-cap-customer-bet-mutations @octocat");

        Assert.Equal(["Flutter-Global/maintainers-cap-customer-bet-mutations", "octocat"], entries[0].Owners);
    }

    [Fact]
    public void Parse_SkipsPatternsWithNoOwners()
    {
        var entries = CodeOwnersParser.Parse("/src/legacy/\n* @global-owner");

        Assert.Single(entries);
        Assert.Equal("*", entries[0].Pattern);
    }

    [Fact]
    public void Parse_EmptyOrNullContent_ReturnsEmpty()
    {
        Assert.Empty(CodeOwnersParser.Parse(null));
        Assert.Empty(CodeOwnersParser.Parse("   "));
    }
}

public class CodeOwnersMatcherTests
{
    [Fact]
    public void MatchOwners_LastMatchingRuleWins()
    {
        var entries = CodeOwnersParser.Parse("""
            * @Flutter-Global/global-owners
            /src/payments/ @Flutter-Global/maintainers-cap-customer-bet-mutations
            """);

        var owners = CodeOwnersMatcher.MatchOwners(entries, ["src/payments/routing.cs"]);

        Assert.Equal(["Flutter-Global/maintainers-cap-customer-bet-mutations"], owners);
    }

    [Fact]
    public void MatchOwners_UnrootedPatternMatchesAtAnyDepth()
    {
        var entries = CodeOwnersParser.Parse("*.md @docs-team");

        var owners = CodeOwnersMatcher.MatchOwners(entries, ["a/b/c/readme.md"]);

        Assert.Equal(["docs-team"], owners);
    }

    [Fact]
    public void MatchOwners_DirectoryPatternMatchesNestedFiles()
    {
        var entries = CodeOwnersParser.Parse("/src/payments/ @payments-team");

        var owners = CodeOwnersMatcher.MatchOwners(entries, ["src/payments/nested/deep/file.cs"]);

        Assert.Equal(["payments-team"], owners);
    }

    [Fact]
    public void MatchOwners_NoMatchingFiles_ReturnsEmpty()
    {
        var entries = CodeOwnersParser.Parse("/src/payments/ @payments-team");

        var owners = CodeOwnersMatcher.MatchOwners(entries, ["docs/readme.md"]);

        Assert.Empty(owners);
    }

    [Fact]
    public void MatchOwners_MultipleChangedFiles_UnionsDistinctOwners()
    {
        var entries = CodeOwnersParser.Parse("""
            /src/payments/ @payments-team
            /src/billing/ @billing-team
            """);

        var owners = CodeOwnersMatcher.MatchOwners(entries, ["src/payments/a.cs", "src/billing/b.cs", "src/payments/c.cs"]);

        Assert.Equal(2, owners.Count);
        Assert.Contains("payments-team", owners);
        Assert.Contains("billing-team", owners);
    }

    [Fact]
    public void MatchOwners_NoEntriesOrNoFiles_ReturnsEmpty()
    {
        Assert.Empty(CodeOwnersMatcher.MatchOwners([], ["a.cs"]));
        Assert.Empty(CodeOwnersMatcher.MatchOwners(CodeOwnersParser.Parse("* @a"), []));
    }
}
