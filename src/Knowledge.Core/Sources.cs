namespace Knowledge.Core;

/// <summary>Read-only access to a repository's files (L1). Paths are relative and use '/'.</summary>
public interface IRepoContextSource
{
    /// <summary>Returns the file text, or null when the file does not exist.</summary>
    Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken);

    /// <summary>Returns the relative paths of files directly inside a directory (empty when it does not exist).</summary>
    Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, CancellationToken cancellationToken);
}

public sealed record WorkItemComment(DateTimeOffset CreatedAt, string Text);

public sealed record WorkItem(
    int Id,
    string Title,
    string State,
    string? Description,
    string? AcceptanceCriteria,
    IReadOnlyList<WorkItemComment> Comments,
    IReadOnlyList<string> Links);

/// <summary>Reads work items on behalf of the caller (L4). Returns null when it does not exist or the caller may not read it.</summary>
public interface IWorkItemSource
{
    Task<WorkItem?> GetAsync(CallerContext caller, int id, CancellationToken cancellationToken);
}

/// <summary>Obtains a token for a downstream API on behalf of the caller (OBO), or for the gateway itself.</summary>
public interface IDownstreamTokenProvider
{
    Task<string> GetTokenAsync(CallerContext caller, string scope, CancellationToken cancellationToken);
}
