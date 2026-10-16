using System.Text;

namespace Knowledge.Application.RepoContext;

public sealed record GlossaryEntry(string Term, string Definition, string? InCode, string? NotToBeConfusedWith)
{
    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("**").Append(Term).Append("**: ").AppendLine(Definition);
        if (InCode is not null)
        {
            text.Append("- In code: `").Append(InCode).AppendLine("`");
        }

        if (NotToBeConfusedWith is not null)
        {
            text.Append("- Not to be confused with: ").AppendLine(NotToBeConfusedWith);
        }

        return text.ToString().TrimEnd();
    }
}

public sealed record GlossaryLookup(IReadOnlyList<GlossaryEntry> Matches, IReadOnlyList<string> Suggestions);

/// <summary>The business vocabulary of a repository, read from <c>docs/domain/glossary.md</c>.</summary>
public sealed class Glossary
{
    private const int MaxSuggestions = 3;

    private readonly IReadOnlyList<GlossaryEntry> _entries;

    private Glossary(IReadOnlyList<GlossaryEntry> entries) => _entries = entries;

    public static Glossary Parse(string markdown)
    {
        var entries = new List<GlossaryEntry>();

        foreach (var line in markdown.Split('\n'))
        {
            var cells = SplitRow(line);
            if (cells is null || cells.Count < 2 || IsHeaderOrSeparator(cells))
            {
                continue;
            }

            var term = Clean(cells[0]);
            if (term.Length == 0)
            {
                continue;
            }

            entries.Add(new GlossaryEntry(
                term,
                cells[1].Trim(),
                cells.Count > 2 ? NullIfEmpty(cells[2].Trim().Trim('`')) : null,
                cells.Count > 3 ? NullIfEmpty(cells[3].Trim()) : null));
        }

        return new Glossary(entries);
    }

    public GlossaryLookup Find(string term)
    {
        var wanted = Normalize(term);

        var exact = _entries.Where(e => Normalize(e.Term) == wanted).ToList();
        if (exact.Count > 0)
        {
            return new GlossaryLookup(exact, []);
        }

        var partial = _entries
            .Where(e => Normalize(e.Term).Contains(wanted)
                || (Normalize(e.Term).Length >= 4 && wanted.Contains(Normalize(e.Term))))
            .ToList();
        if (partial.Count > 0)
        {
            return new GlossaryLookup(partial, []);
        }

        var closest = _entries
            .Select(e => (e.Term, Distance: EditDistance(Normalize(e.Term), wanted)))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Term, StringComparer.Ordinal)
            .Take(MaxSuggestions)
            .Select(x => x.Term)
            .ToList();

        return new GlossaryLookup([], closest);
    }

    private static List<string>? SplitRow(string line)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('|'))
        {
            return null;
        }

        return trimmed.Trim('|').Split('|').Select(c => c.Trim()).ToList();
    }

    private static bool IsHeaderOrSeparator(List<string> cells)
    {
        if (cells.All(c => c.Length > 0 && c.All(ch => ch is '-' or ':' or ' ')))
        {
            return true;
        }

        return cells[0].Equals("Term", StringComparison.OrdinalIgnoreCase)
            || cells[0].Equals("Acronym", StringComparison.OrdinalIgnoreCase);
    }

    private static string Clean(string cell) => cell.Replace("*", string.Empty).Replace("`", string.Empty).Trim();

    private static string Normalize(string value) => Clean(value).ToLowerInvariant();

    private static string? NullIfEmpty(string value) =>
        value.Length == 0 || value is "—" or "-" ? null : value;

    private static int EditDistance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
