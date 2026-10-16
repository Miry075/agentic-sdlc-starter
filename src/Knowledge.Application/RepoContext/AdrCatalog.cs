using System.Text;
using System.Text.RegularExpressions;

namespace Knowledge.Application.RepoContext;

public sealed record AdrRecord(int Number, string Title, string Status, string Decision, string FullText)
{
    public string Label => $"ADR {Number:D4}";
}

public sealed record AdrSearchResult(IReadOnlyList<AdrRecord> Records, bool MatchedByNumber);

/// <summary>Architecture decision records of a repository, searched lexically.</summary>
public sealed partial class AdrCatalog
{
    public const int MaxResults = 5;

    private readonly IReadOnlyList<AdrRecord> _records;

    public AdrCatalog(IEnumerable<AdrRecord> records) => _records = records.OrderBy(r => r.Number).ToList();

    [GeneratedRegex(@"^(\d{4})-.+\.md$", RegexOptions.IgnoreCase)]
    private static partial Regex FileNamePattern();

    [GeneratedRegex(@"^(?:adr[-\s]?)?(\d{1,4})$", RegexOptions.IgnoreCase)]
    private static partial Regex NumberQuery();

    public static bool IsAdrFile(string fileName)
    {
        var match = FileNamePattern().Match(fileName);
        return match.Success && match.Groups[1].Value != "0000";
    }

    /// <summary>Returns null for the template and for files that are not ADRs.</summary>
    public static AdrRecord? ParseRecord(string fileName, string markdown)
    {
        if (!IsAdrFile(fileName))
        {
            return null;
        }

        var number = int.Parse(FileNamePattern().Match(fileName).Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var lines = markdown.Replace("\r", string.Empty).Split('\n');

        var heading = lines.FirstOrDefault(l => l.StartsWith("# ", StringComparison.Ordinal)) ?? $"# {number:D4}";
        var title = Regex.Replace(heading[2..].Trim(), @"^\d{4}\.?\s*", string.Empty);

        var status = Section(lines, "Status").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? "Unknown";
        var decision = Section(lines, "Decision").Trim();

        return new AdrRecord(number, title, status, decision, markdown.Trim());
    }

    public AdrSearchResult Search(string query, string? status)
    {
        var candidates = _records
            .Where(r => status is null || r.Status.StartsWith(status, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var numberMatch = NumberQuery().Match(query.Trim());
        if (numberMatch.Success)
        {
            var number = int.Parse(numberMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var exact = candidates.Where(r => r.Number == number).ToList();
            if (exact.Count > 0)
            {
                return new AdrSearchResult(exact, true);
            }
        }

        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var ranked = candidates
            .Select(r => (Record: r, Score: Score(r, terms)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Record.Number)
            .Take(MaxResults)
            .Select(x => x.Record)
            .ToList();

        return new AdrSearchResult(ranked, false);
    }

    private static int Score(AdrRecord record, string[] terms)
    {
        var title = record.Title.ToLowerInvariant();
        var body = record.FullText.ToLowerInvariant();
        var score = 0;

        foreach (var term in terms)
        {
            if (title.Contains(term))
            {
                score += 5;
            }

            score += Math.Min(CountOccurrences(body, term), 5);
        }

        return score;
    }

    private static int CountOccurrences(string text, string term)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += term.Length;
        }

        return count;
    }

    private static string Section(string[] lines, string name)
    {
        var text = new StringBuilder();
        var inside = false;

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                if (inside)
                {
                    break;
                }

                inside = line[3..].Trim().Equals(name, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inside)
            {
                text.AppendLine(line);
            }
        }

        return text.ToString();
    }
}
