using System.Text.RegularExpressions;

namespace GithubScout.Core.Services;

/// <summary>
/// Matches changed file paths against parsed CODEOWNERS rules, following GitHub's own
/// "last matching rule wins" semantics, and reports the resulting distinct owners.
/// </summary>
public static class CodeOwnersMatcher
{
    /// <summary>
    /// Returns the distinct set of owners (teams as "org/slug", users without the leading "@")
    /// required across all of the given changed file paths.
    /// </summary>
    public static IReadOnlyList<string> MatchOwners(IReadOnlyList<CodeOwnersEntry> entries, IReadOnlyList<string> changedFilePaths)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(changedFilePaths);

        if (entries.Count == 0 || changedFilePaths.Count == 0)
        {
            return Array.Empty<string>();
        }

        // Pre-compile patterns once; CODEOWNERS uses "last match wins" so we walk entries in
        // order and remember the last one that matched each file.
        var compiled = entries
            .Select(e => (e.Owners, Regex: ToRegex(e.Pattern)))
            .ToList();

        var owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in changedFilePaths)
        {
            var normalizedPath = "/" + path.TrimStart('/');
            IReadOnlyList<string>? matchedOwners = null;

            foreach (var (entryOwners, regex) in compiled)
            {
                if (regex.IsMatch(normalizedPath))
                {
                    matchedOwners = entryOwners;
                }
            }

            if (matchedOwners is not null)
            {
                foreach (var owner in matchedOwners)
                {
                    owners.Add(owner);
                }
            }
        }

        return owners.ToList();
    }

    /// <summary>Converts a (simplified) gitignore-style CODEOWNERS pattern into an anchored regex.</summary>
    private static Regex ToRegex(string pattern)
    {
        var isRooted = pattern.StartsWith('/');
        var isDirectory = pattern.EndsWith('/');

        var trimmed = pattern.Trim('/');
        var segments = trimmed.Split('/');

        var regexParts = segments.Select(segment =>
        {
            if (segment == "**")
            {
                return ".*";
            }

            var escaped = Regex.Escape(segment)
                .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
                .Replace(@"\?", "[^/]", StringComparison.Ordinal);
            return escaped;
        });

        var joined = string.Join("/", regexParts);
        var body = isDirectory ? $"{joined}(/.*)?" : joined;

        // Unrooted patterns (no leading '/') may match starting at any directory level.
        var prefix = isRooted ? "^/" : "^/(.*/)?";
        return new Regex($"{prefix}{body}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}
