namespace GithubScout.Core.Services;

/// <summary>A single parsed CODEOWNERS rule: a path pattern and the owners responsible for it.</summary>
public sealed record CodeOwnersEntry(string Pattern, IReadOnlyList<string> Owners);

/// <summary>
/// Parses a CODEOWNERS file's contents into ordered rules. Blank lines and comments (starting
/// with '#') are ignored. Order is preserved because CODEOWNERS uses "last matching rule wins".
/// </summary>
public static class CodeOwnersParser
{
    public static IReadOnlyList<CodeOwnersEntry> Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<CodeOwnersEntry>();
        }

        var entries = new List<CodeOwnersEntry>();

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                // A pattern with no owners means "no one owns this path" - nothing to report.
                continue;
            }

            var pattern = parts[0];
            var owners = parts.Skip(1).Select(o => o.TrimStart('@')).ToList();
            entries.Add(new CodeOwnersEntry(pattern, owners));
        }

        return entries;
    }
}
