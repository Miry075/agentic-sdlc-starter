namespace AiReadiness.Scanner.Domain;

public sealed record RepoEntry(string Name, IRepoFiles Files);

/// <summary>Where repositories come from: one folder, or every repository inside a parent folder.</summary>
public interface IRepoOpener
{
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    RepoEntry Open(string path);

    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    IReadOnlyList<RepoEntry> OpenAll(string parent);
}
