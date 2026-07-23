using System.Text.RegularExpressions;
using GithubScout.Core.Models;

namespace GithubScout.Core.Services;

/// <summary>
/// Parses a plain GitHub URL (pull request or branch) into a <see cref="WatchItem"/>, so users
/// can add a watch item by simply pasting a link instead of typing owner/repo/number by hand.
/// </summary>
public static partial class GitHubUrlParser
{
    // https://github.com/{owner}/{repo}/pull/{number}(/...)
    [GeneratedRegex(@"^https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/pull/(?<number>\d+)(?:/.*)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex PullRequestUrlRegex();

    // https://github.com/{owner}/{repo}/tree/{branch} - branch names may themselves contain slashes.
    [GeneratedRegex(@"^https?://github\.com/(?<owner>[^/]+)/(?<repo>[^/]+)/tree/(?<branch>.+?)/?$", RegexOptions.IgnoreCase)]
    private static partial Regex BranchUrlRegex();

    /// <summary>
    /// Attempts to parse a GitHub PR or branch URL. Returns a <see cref="WatchItem"/> with
    /// <see cref="WatchItem.Id"/> already computed and <see cref="WatchItem.Alias"/> unset.
    /// </summary>
    public static bool TryParse(string? url, out WatchItem? item)
    {
        item = null;

        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();

        var prMatch = PullRequestUrlRegex().Match(trimmed);
        if (prMatch.Success)
        {
            var owner = prMatch.Groups["owner"].Value;
            var repo = prMatch.Groups["repo"].Value;
            var number = int.Parse(prMatch.Groups["number"].Value);

            item = new WatchItem
            {
                Id = WatchItem.BuildId(owner, repo, WatchItemKind.PullRequest, null, number),
                Owner = owner,
                Repo = repo,
                Kind = WatchItemKind.PullRequest,
                PullRequestNumber = number
            };
            return true;
        }

        var branchMatch = BranchUrlRegex().Match(trimmed);
        if (branchMatch.Success)
        {
            var owner = branchMatch.Groups["owner"].Value;
            var repo = branchMatch.Groups["repo"].Value;
            var branch = branchMatch.Groups["branch"].Value;

            item = new WatchItem
            {
                Id = WatchItem.BuildId(owner, repo, WatchItemKind.Branch, branch, null),
                Owner = owner,
                Repo = repo,
                Kind = WatchItemKind.Branch,
                BranchName = branch
            };
            return true;
        }

        return false;
    }
}
