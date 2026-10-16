using System.Text.RegularExpressions;
using Knowledge.Core;

namespace Knowledge.Redaction;

/// <summary>Masks sensitive identifiers in text. Defence in depth: sources are also filtered before this point.</summary>
public sealed class PatternRedactor : IDataRedactor
{
    public const string WithheldNotice = "[content withheld: redaction could not complete]";

    private readonly IReadOnlyList<RedactionRule> _rules;

    public PatternRedactor(IEnumerable<RedactionRule> rules) => _rules = rules.ToList();

    /// <summary>The built-in rules plus the organisation's own, each of which must prove itself on a sample.</summary>
    public static PatternRedactor Create(IEnumerable<CustomRedactionPattern>? custom = null)
    {
        var rules = DefaultRules.Create().ToList();

        foreach (var pattern in custom ?? [])
        {
            var rule = pattern.ToRule();
            rule.Apply(pattern.Sample, out var replaced);
            if (replaced == 0)
            {
                throw new InvalidOperationException(
                    $"Custom redaction pattern '{pattern.Kind}' does not match its own sample.");
            }

            rules.Add(rule);
        }

        return new PatternRedactor(rules);
    }

    public RedactionResult Redact(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new RedactionResult(text ?? string.Empty, 0, new Dictionary<string, int>());
        }

        var counts = new Dictionary<string, int>();
        var current = text;

        try
        {
            foreach (var rule in _rules)
            {
                current = rule.Apply(current, out var replaced);
                if (replaced > 0)
                {
                    counts[rule.Kind] = counts.GetValueOrDefault(rule.Kind) + replaced;
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail closed: never return text that may still hold what the rule was meant to remove.
            return new RedactionResult(WithheldNotice, 1, new Dictionary<string, int> { ["WITHHELD"] = 1 });
        }

        return new RedactionResult(current, counts.Values.Sum(), counts);
    }
}

/// <summary>An organisation-specific pattern from configuration. The sample proves the pattern works.</summary>
public sealed record CustomRedactionPattern(string Kind, string Pattern, string Sample)
{
    internal RedactionRule ToRule() => new(Kind.ToUpperInvariant(), Pattern);
}
