using System.Text;
using AiReadiness.Scanner.Remediation;

namespace AiReadiness.Scanner.Files;

/// <summary>Creates new files inside a repository folder. Never overwrites, never leaves the root, never writes through a symbolic link.</summary>
public sealed class DiskRepoWriter(string root) : IRepoWriter
{
    private readonly string _root = Path.GetFullPath(root);

    public void CreateAll(IReadOnlyList<FileChange> changes)
    {
        var targets = changes.Select(c => (Change: c, Full: Resolve(c.Path))).ToList();

        foreach (var (change, full) in targets)
        {
            if (File.Exists(full) || Directory.Exists(full))
            {
                throw new FixConflictException($"{change.Path} already exists. Nothing was written.");
            }
        }

        foreach (var (change, full) in targets)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, change.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (change.Executable && !OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(full, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                                           | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                                           | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
    }

    private string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new FixConflictException($"{relative} is outside the repository. Nothing was written.");
        }

        for (var dir = new DirectoryInfo(Path.GetDirectoryName(full)!); dir is not null && dir.FullName.Length > _root.Length; dir = dir.Parent)
        {
            if (dir.Exists && dir.LinkTarget is not null)
            {
                throw new FixConflictException($"{relative} goes through a symbolic link. Nothing was written.");
            }
        }

        return full;
    }
}
