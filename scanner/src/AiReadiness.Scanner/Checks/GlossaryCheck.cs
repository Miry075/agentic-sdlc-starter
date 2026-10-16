using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class GlossaryCheck : IReadinessCheck
{
    public const string Path = "docs/domain/glossary.md";

    public string Area => "Domain glossary";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var text = repo.ReadText(Path);
        var terms = text is null ? 0 : CountTerms(text);

        return
        [
            new(Area, "docs/domain/glossary.md exists", 5, text is not null,
                "Create docs/domain/glossary.md. Business terms and acronyms are what agents get wrong most."),
            new(Area, "At least 5 real terms", 6, terms >= 5,
                "Define at least 5 terms as table rows or bullets starting with a bold term, for example `| **Credit note** | ... |`."),
            new(Area, "At least 15 real terms", 4, terms >= 15,
                "Grow the glossary to 15 terms. Start with every acronym and internal product name that appears in code."),
        ];
    }

    private static int CountTerms(string text) =>
        text.Split('\n').Count(line => TermPattern().IsMatch(line) && !line.Contains('<'));

    [GeneratedRegex(@"^\s*(\||-)\s*\*\*[^*]+\*\*")]
    private static partial Regex TermPattern();
}
