using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Knowledge.Core;

namespace Knowledge.Adapters.RepoContext;

public sealed class AzureReposOptions
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;

    /// <summary>The default branch. Only this branch is ever read: no feature branches, no history.</summary>
    public string Branch { get; set; } = "main";

    public int CacheSeconds { get; set; } = 60;
}

/// <summary>
/// Reads files from the default branch of an Azure Repos repository with the gateway's own identity.
/// Responses are cached briefly; the cache is keyed by path only (not by commit), which is simpler than
/// the design and good enough for 60 seconds.
/// </summary>
public sealed class AzureReposSource(
    HttpClient http,
    IServiceTokenProvider tokens,
    AzureReposOptions options,
    TimeProvider clock) : IRepoContextSource
{
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";
    private const string ApiVersion = "7.1";

    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, string? Text)> _files = new();
    private readonly ConcurrentDictionary<string, (DateTimeOffset ExpiresAt, IReadOnlyList<string> Paths)> _listings = new();

    public async Task<string?> ReadTextAsync(string relativePath, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_files.TryGetValue(relativePath, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Text;
        }

        var url = $"{BaseUrl()}/items?path=/{Uri.EscapeDataString(relativePath).Replace("%2F", "/")}" +
                  $"&versionDescriptor.version={Uri.EscapeDataString(options.Branch)}&versionDescriptor.versionType=branch" +
                  $"&includeContent=true&api-version={ApiVersion}";

        using var response = await SendAsync(url, "text/plain", cancellationToken);
        var text = response is null ? null : await response.Content.ReadAsStringAsync(cancellationToken);

        _files[relativePath] = (now.AddSeconds(options.CacheSeconds), text);
        return text;
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync(string relativeDirectory, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (_listings.TryGetValue(relativeDirectory, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Paths;
        }

        var url = $"{BaseUrl()}/items?scopePath=/{Uri.EscapeDataString(relativeDirectory).Replace("%2F", "/")}" +
                  $"&recursionLevel=OneLevel&versionDescriptor.version={Uri.EscapeDataString(options.Branch)}" +
                  $"&versionDescriptor.versionType=branch&api-version={ApiVersion}";

        using var response = await SendAsync(url, "application/json", cancellationToken);
        var paths = new List<string>();

        if (response is not null)
        {
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            foreach (var item in json.RootElement.GetProperty("value").EnumerateArray())
            {
                var isFolder = item.TryGetProperty("isFolder", out var folder) && folder.GetBoolean();
                if (!isFolder && item.TryGetProperty("path", out var path) && path.GetString() is { } value)
                {
                    paths.Add(value.TrimStart('/'));
                }
            }
        }

        _listings[relativeDirectory] = (now.AddSeconds(options.CacheSeconds), paths);
        return paths;
    }

    private string BaseUrl() =>
        $"https://dev.azure.com/{Uri.EscapeDataString(options.Organization)}/{Uri.EscapeDataString(options.Project)}" +
        $"/_apis/git/repositories/{Uri.EscapeDataString(options.Repository)}";

    /// <summary>Returns null on 404. Other failures become <see cref="SourceUnavailableException"/>.</summary>
    private async Task<HttpResponseMessage?> SendAsync(string url, string accept, CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokens.GetTokenAsync(AzureDevOpsScope, cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

            var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                response.Dispose();
                return null;
            }

            response.EnsureSuccessStatusCode();
            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new SourceUnavailableException("azure-repos", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceUnavailableException("azure-repos", ex);
        }
    }
}
