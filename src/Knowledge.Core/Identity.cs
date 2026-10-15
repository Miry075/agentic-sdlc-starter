namespace Knowledge.Core;

/// <summary>The authenticated person (or dev stand-in) on whose behalf an agent acts.</summary>
public sealed record CallerContext(string UserId, IReadOnlySet<string> Roles, string AuthMode);
