namespace Knowledge.Gateway.Configuration;

/// <summary>
/// Finds a configured relative path whether the gateway is started from the repository root, from the
/// project folder (dotnet run), or from a container's /app. Absolute paths are used as given.
/// </summary>
public static class PathResolver
{
    public static string Resolve(string path, params string[] startingPoints)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var candidates = new[] { Directory.GetCurrentDirectory() }.Concat(startingPoints);
        foreach (var start in candidates)
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, path);
                if (File.Exists(candidate) || Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        // Leave it as given: the error that follows names the path the operator configured.
        return path;
    }
}
