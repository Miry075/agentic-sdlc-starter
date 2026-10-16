using System.Text.RegularExpressions;

namespace Knowledge.Redaction;

/// <summary>
/// One kind of sensitive value: a pattern, and an optional check (checksum) that a match must pass
/// before it is replaced. The check keeps false positives low.
/// </summary>
public sealed class RedactionRule
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    private readonly Regex _pattern;
    private readonly Func<string, bool>? _isValid;

    public RedactionRule(string kind, string pattern, Func<string, bool>? isValid = null, RegexOptions options = RegexOptions.None)
    {
        Kind = kind;
        _isValid = isValid;
        _pattern = new Regex(pattern, options | RegexOptions.CultureInvariant, MatchTimeout);
    }

    public string Kind { get; }

    public string Placeholder => $"[{Kind}]";

    /// <summary>Replaces valid matches and reports how many were replaced.</summary>
    /// <exception cref="RegexMatchTimeoutException">The pattern took too long. The caller must fail closed.</exception>
    public string Apply(string text, out int replaced)
    {
        var count = 0;
        var result = _pattern.Replace(text, match =>
        {
            if (_isValid is not null && !_isValid(match.Value))
            {
                return match.Value;
            }

            count++;
            return Placeholder;
        });

        replaced = count;
        return result;
    }
}
