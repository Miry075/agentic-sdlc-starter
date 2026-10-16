using Knowledge.Core;

namespace Knowledge.Adapters.RepoContext;

/// <summary>A repository on local disk. Used in dev mode with the sample repo. Never reads outside its root.</summary>
public sealed class FileSystemRepoSource : IRepoContextSource
{
    private readonly string _root;

    public FileSystemRepoSource(string root) => _root = Path.GetFullPath(root);

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        var full = Resolve(relativePath);
        var file = new FileInfo(full);

        // A link could point outside the root, so links are treated as missing.
        if (!file.Exists || file.LinkTarget is not null)
        {
            return null;
        }

        return await File.ReadAllTextAsync(full, cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        var full = Resolve(relativeDirectory);
        if (!Directory.Exists(full))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        IReadOnlyList<string> files = Directory.EnumerateFiles(full)
            .Where(path => new FileInfo(path).LinkTarget is null)
            .Select(path => Path.GetRelativePath(_root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(files);
    }

    private string Resolve(string relativePath)
    {
        var segments = relativePath.Split('/', '\\');
        if (Path.IsPathRooted(relativePath) || segments.Contains(".."))
        {
            throw new ArgumentException("The path must be relative and stay inside the repository.", nameof(relativePath));
        }

        var full = Path.GetFullPath(Path.Combine(_root, relativePath));
        var inside = full == _root || full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal);

        return inside
            ? full
            : throw new ArgumentException("The path must stay inside the repository.", nameof(relativePath));
    }
}
