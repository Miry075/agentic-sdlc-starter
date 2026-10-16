namespace AiReadiness.Scanner.Domain;

/// <summary>Read-only view of a repository. Paths are relative to the repository root and use '/'.</summary>
public interface IRepoFiles
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>The file's text, or null when it does not exist or is too large to be a text document.</summary>
    string? ReadText(string path);

    /// <summary>Every file under <paramref name="directory"/>, recursively. Use "" for the whole repository.</summary>
    IReadOnlyList<string> Files(string directory);
}
