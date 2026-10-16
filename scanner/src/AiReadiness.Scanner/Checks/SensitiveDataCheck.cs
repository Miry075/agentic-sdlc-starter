using System.Text.RegularExpressions;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed partial class SensitiveDataCheck : IReadinessCheck
{
    public string Area => "Sensitive data protection";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var all = repo.Files(string.Empty);
        var secretFiles = all.Where(IsSecretFile).ToList();
        var leaking = all.Where(IsConfigFile).Where(p => repo.ReadText(p) is { } t && SecretValue().IsMatch(t)).ToList();
        var gitignore = repo.ReadText(".gitignore");
        var settings = repo.ReadText(".claude/settings.json") ?? string.Empty;

        return
        [
            new(Area, "No secret files committed (.env, .pfx, private keys)", 8, secretFiles.Count == 0,
                $"Remove from Git and rotate: {string.Join(", ", secretFiles.Take(3))}. Keep a `.env.example` with placeholders instead."),
            new(Area, "No passwords or keys in config files", 6, leaking.Count == 0,
                $"Move the values to Key Vault or user secrets and leave placeholders. Found in: {string.Join(", ", leaking.Take(3))}."),
            new(Area, ".gitignore excludes .env files", 3, gitignore is not null && EnvIgnored().IsMatch(gitignore),
                "Add `.env` and `.env.*` to .gitignore."),
            new(Area, "Agent deny rules cover secrets (.claude/settings.json)", 3,
                settings.Contains("\"deny\"", StringComparison.Ordinal) && settings.Contains(".env", StringComparison.Ordinal),
                "Add permissions.deny rules for .env, secrets, seeds and test data in .claude/settings.json."),
        ];
    }

    private static bool IsSecretFile(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        if (name.EndsWith(".example", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".sample", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Equals(".env", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith(".env.", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".p12", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfigFile(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
               || name.EndsWith(".config", StringComparison.OrdinalIgnoreCase)
               || name.Equals("local.settings.json", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(Password|Pwd|AccountKey|SharedAccessKey)\s*=\s*(?![<{$%*])[^;""\s]{4,}", RegexOptions.IgnoreCase)]
    private static partial Regex SecretValue();

    [GeneratedRegex(@"^\s*/?\.env(\*|\.\*)?\s*$", RegexOptions.Multiline)]
    private static partial Regex EnvIgnored();
}
