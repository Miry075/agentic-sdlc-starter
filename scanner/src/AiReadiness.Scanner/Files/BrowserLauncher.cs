using System.ComponentModel;
using System.Diagnostics;

namespace AiReadiness.Scanner.Files;

/// <summary>Opens a report in the default browser.</summary>
public interface IBrowserLauncher
{
    /// <returns>False when the report could not be opened. The scan itself is never affected.</returns>
    bool TryOpen(string path);
}

public sealed class SystemBrowserLauncher : IBrowserLauncher
{
    public bool TryOpen(string path)
    {
        try
        {
            using var _ = Process.Start(StartInfo(Path.GetFullPath(path)));
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static ProcessStartInfo StartInfo(string fullPath)
    {
        if (OperatingSystem.IsWindows())
        {
            return new ProcessStartInfo(fullPath) { UseShellExecute = true };
        }

        // The path goes in as one argument, never through a shell.
        var info = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add(fullPath);
        return info;
    }
}

/// <summary>Opening a browser is for a person at a terminal, not for CI logs or pipes.</summary>
public static class AutoOpen
{
    private static readonly string[] CiVariables = ["CI", "TF_BUILD", "GITHUB_ACTIONS"];

    public static bool IsAllowed(bool outputRedirected, Func<string, string?> environment) =>
        !outputRedirected && CiVariables.All(name => string.IsNullOrEmpty(environment(name)));
}
