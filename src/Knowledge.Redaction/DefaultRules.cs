using System.Text.RegularExpressions;

namespace Knowledge.Redaction;

/// <summary>
/// The built-in rules. Order matters: long, specific patterns first so that a connection string
/// is replaced as a whole before its password is, and phone numbers last so they do not eat
/// digits that belong to a card or a national number.
/// </summary>
public static class DefaultRules
{
    public static IReadOnlyList<RedactionRule> Create() =>
    [
        new RedactionRule(
            "CONNECTION_STRING",
            @"\b(?:Server|Data Source|DefaultEndpointsProtocol|AccountName|Endpoint)\s*=\s*[^;\r\n]+;(?:\s*[A-Za-z][\w ]*=[^;\r\n]*;?)*|\bAccountKey\s*=\s*[^;\s]+",
            options: RegexOptions.IgnoreCase),

        new RedactionRule("TOKEN", @"\beyJ[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]{5,}\.[A-Za-z0-9_-]*"),

        new RedactionRule(
            "SECRET",
            @"\bsk-[A-Za-z0-9_-]{20,}|\bghp_[A-Za-z0-9]{20,}|\bAKIA[0-9A-Z]{16}\b|\bxox[abprs]-[A-Za-z0-9-]{10,}|\b(?i:password|pwd)\s*=\s*[^\s;,""']+",
            options: RegexOptions.None),

        new RedactionRule("EMAIL", @"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}"),

        new RedactionRule(
            "IBAN",
            @"\b[A-Z]{2}\d{2}(?: ?[A-Z0-9]{4}){2,7}(?: ?[A-Z0-9]{1,3})?\b",
            Checksums.IsValidIban),

        new RedactionRule(
            "NATIONAL_ID",
            @"\b\d{2}\.\d{2}\.\d{2}-\d{3}\.\d{2}\b",
            Checksums.IsValidBelgianNationalNumber),

        new RedactionRule(
            "CARD",
            @"\b(?:\d[ -]?){12,18}\d\b",
            Checksums.IsValidCardNumber),

        new RedactionRule(
            "PHONE",
            @"(?<![\w+])(?:\+|00)[1-9]\d{0,2}(?:[\s./-]?\d{1,4}){2,6}(?!\d)|\b0[1-9]\d{0,2}[\s./-]\d{2,3}(?:[\s./-]?\d{2}){2}\b|\b04\d{8}\b"),
    ];
}
