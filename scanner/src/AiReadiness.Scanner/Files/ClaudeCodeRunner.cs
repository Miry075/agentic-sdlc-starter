using System.Diagnostics;

namespace AiReadiness.Scanner.Files;

/// <summary>Runs an AI coding agent in a repository. The scanner itself never calls a model; only the agent command does.</summary>
public interface IAgentRunner
{
    bool IsAvailable();

    /// <param name="onOutputLine">Called for every line the agent writes to standard output, as it arrives.</param>
    /// <returns>The agent's exit code.</returns>
    int Run(string workingDirectory, IReadOnlyList<string> arguments, Action<string> onOutputLine);
}

/// <summary>Claude Code's CLI (`claude`). Standard output is read line by line so progress can be shown live; errors go straight to the terminal.</summary>
public sealed class ClaudeCodeRunner : IAgentRunner
{
    public bool IsAvailable() => Find() is not null;

    public int Run(string workingDirectory, IReadOnlyList<string> arguments, Action<string> onOutputLine)
    {
        var executable = Find() ?? throw new InvalidOperationException("Claude Code is not on PATH.");
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Claude Code did not start.");
        process.StandardInput.Close(); // nothing to pipe in; Claude Code must not wait for stdin
        while (process.StandardOutput.ReadLine() is { } line)
        {
            onOutputLine(line);
        }

        process.WaitForExit();
        return process.ExitCode;
    }

    private static string? Find()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "claude.exe", "claude.cmd" } : ["claude"];
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(dir => names.Select(name => Path.Combine(dir, name)))
            .FirstOrDefault(File.Exists);
    }
}
