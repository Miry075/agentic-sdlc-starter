using System.Diagnostics;

namespace AiReadiness.Scanner.Files;

/// <summary>The few Git operations `ai-readiness agent` needs to enforce the branch policy.</summary>
public interface IGit
{
    /// <summary>True when there are no uncommitted or untracked changes.</summary>
    bool IsClean(string repo);

    string? CurrentBranch(string repo);

    /// <summary>The branch origin/HEAD points to, else main or master when they exist.</summary>
    string? DefaultBranch(string repo);

    bool BranchExists(string repo, string branch);

    /// <summary>The commit a branch points to, or null.</summary>
    string? Tip(string repo, string branch);

    /// <summary>Creates <paramref name="branch"/> from <paramref name="from"/> and switches to it.</summary>
    bool CreateAndSwitch(string repo, string branch, string from);

    /// <summary>Stages one file and commits it on the current branch.</summary>
    bool CommitFile(string repo, string path, string message);

    /// <summary>"azure", "github" or null, from the origin URL. The URL itself is never printed: it can hold a token.</summary>
    string? Host(string repo);
}

/// <summary>Runs the `git` command line.</summary>
public sealed class GitCli : IGit
{
    public bool IsClean(string repo) => Run(repo, "status", "--porcelain") is { Code: 0, Output: var o } && o.Trim().Length == 0;

    public string? CurrentBranch(string repo) => Value(Run(repo, "branch", "--show-current"));

    public string? DefaultBranch(string repo)
    {
        if (Value(Run(repo, "symbolic-ref", "--quiet", "--short", "refs/remotes/origin/HEAD")) is { } remoteHead)
        {
            return remoteHead.StartsWith("origin/", StringComparison.Ordinal) ? remoteHead["origin/".Length..] : remoteHead;
        }

        return new[] { "main", "master" }.FirstOrDefault(b => BranchExists(repo, b));
    }

    public bool BranchExists(string repo, string branch) =>
        Run(repo, "show-ref", "--verify", "--quiet", $"refs/heads/{branch}").Code == 0;

    public string? Tip(string repo, string branch) => Value(Run(repo, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"));

    public bool CreateAndSwitch(string repo, string branch, string from) => Run(repo, "switch", "-c", branch, from).Code == 0;

    public bool CommitFile(string repo, string path, string message) =>
        Run(repo, "add", "--", path).Code == 0 && Run(repo, "commit", "-m", message, "--", path).Code == 0;

    public string? Host(string repo)
    {
        var url = Value(Run(repo, "remote", "get-url", "origin")) ?? string.Empty;
        if (url.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase) || url.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return "azure";
        }

        return url.Contains("github.com", StringComparison.OrdinalIgnoreCase) ? "github" : null;
    }

    private static string? Value((int Code, string Output) result) =>
        result.Code == 0 && result.Output.Trim() is { Length: > 0 } value ? value : null;

    private static (int Code, string Output) Run(string repo, params string[] args)
    {
        var info = new ProcessStartInfo("git") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("-C");
        info.ArgumentList.Add(repo);
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        try
        {
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, string.Empty);
        }
    }
}
