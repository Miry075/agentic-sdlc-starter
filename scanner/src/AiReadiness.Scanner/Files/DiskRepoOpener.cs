using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Files;

public sealed class DiskRepoOpener : IRepoOpener
{
    public RepoEntry Open(string path)
    {
        var files = new DiskRepoFiles(path);
        return new RepoEntry(new DirectoryInfo(Path.GetFullPath(path)).Name, files);
    }

    /// <summary>Every direct subfolder that is a Git repository (contains .git). Symbolic links are skipped.</summary>
    public IReadOnlyList<RepoEntry> OpenAll(string parent)
    {
        var root = Path.GetFullPath(parent);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"Folder not found: {root}");
        }

        return Directory.EnumerateDirectories(root)
            .Where(d => new DirectoryInfo(d).LinkTarget is null && (Directory.Exists(Path.Combine(d, ".git")) || File.Exists(Path.Combine(d, ".git"))))
            .OrderBy(d => d, StringComparer.Ordinal)
            .Select(Open)
            .ToList();
    }
}
