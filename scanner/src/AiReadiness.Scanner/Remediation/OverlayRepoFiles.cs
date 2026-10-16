using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Remediation;

/// <summary>A repository with planned file creations laid over it, so a plan can be measured without writing anything.</summary>
public sealed class OverlayRepoFiles(IRepoFiles inner, IEnumerable<FileChange> changes) : IRepoFiles
{
    private readonly Dictionary<string, string> _added = changes.ToDictionary(c => c.Path, c => c.Content, StringComparer.Ordinal);

    public bool FileExists(string path) => _added.ContainsKey(path) || inner.FileExists(path);

    public bool DirectoryExists(string path) =>
        inner.DirectoryExists(path) || _added.Keys.Any(k => k.StartsWith(path + "/", StringComparison.Ordinal));

    public string? ReadText(string path) => _added.TryGetValue(path, out var content) ? content : inner.ReadText(path);

    public IReadOnlyList<string> Files(string directory) => inner.Files(directory)
        .Concat(_added.Keys.Where(k => directory.Length == 0 || k.StartsWith(directory + "/", StringComparison.Ordinal)))
        .Distinct(StringComparer.Ordinal)
        .OrderBy(k => k, StringComparer.Ordinal)
        .ToList();
}
