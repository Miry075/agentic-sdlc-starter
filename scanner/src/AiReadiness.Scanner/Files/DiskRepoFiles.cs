using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Files;

/// <summary>Reads a repository from disk. Never leaves the root and never follows symbolic links.</summary>
public sealed class DiskRepoFiles : IRepoFiles
{
    private const long MaxTextBytes = 1024 * 1024;
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea",
    };

    private readonly string _root;

    public DiskRepoFiles(string root)
    {
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException($"Folder not found: {_root}");
        }
    }

    public bool FileExists(string path) => Resolve(path) is { } full && File.Exists(full) && !IsLink(full);

    public bool DirectoryExists(string path) => Resolve(path) is { } full && Directory.Exists(full) && !IsLink(full);

    public string? ReadText(string path)
    {
        if (Resolve(path) is not { } full || !File.Exists(full) || IsLink(full) || new FileInfo(full).Length > MaxTextBytes)
        {
            return null;
        }

        return File.ReadAllText(full);
    }

    public IReadOnlyList<string> Files(string directory)
    {
        if (Resolve(directory) is not { } start || !Directory.Exists(start) || IsLink(start))
        {
            return [];
        }

        var result = new List<string>();
        Walk(start, result);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private void Walk(string directory, List<string> result)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (!IsLink(file))
            {
                result.Add(Relative(file));
            }
        }

        foreach (var child in Directory.EnumerateDirectories(directory))
        {
            if (!SkippedDirectories.Contains(Path.GetFileName(child)) && !IsLink(child))
            {
                Walk(child, result);
            }
        }
    }

    private string? Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        var inside = full.Equals(_root, StringComparison.Ordinal)
                     || full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return inside ? full : null;
    }

    private string Relative(string full) => Path.GetRelativePath(_root, full).Replace('\\', '/');

    private static bool IsLink(string path) => new FileInfo(path).LinkTarget is not null;
}
