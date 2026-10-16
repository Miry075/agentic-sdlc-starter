using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Knowledge.Core;

namespace Knowledge.Adapters.AzureDevOps;

public sealed class AzureDevOpsOptions
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
}

/// <summary>
/// Reads work items as the signed-in user (on-behalf-of), so an agent can never read what its user cannot.
/// A 401, 403 or 404 all mean "not available to you" and return null.
/// </summary>
public sealed class AzureDevOpsWorkItemSource(
    HttpClient http,
    IDownstreamTokenProvider tokens,
    AzureDevOpsOptions options) : IWorkItemSource
{
    private const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

    public async Task<WorkItem?> GetAsync(CallerContext caller, int id, CancellationToken cancellationToken)
    {
        try
        {
            var token = await tokens.GetTokenAsync(caller, AzureDevOpsScope, cancellationToken);

            var baseUrl = $"https://dev.azure.com/{Uri.EscapeDataString(options.Organization)}/{Uri.EscapeDataString(options.Project)}/_apis/wit";

            using var workItem = await GetJsonAsync($"{baseUrl}/workitems/{id}?$expand=relations&api-version=7.1", token, cancellationToken);
            if (workItem is null)
            {
                return null;
            }

            using var comments = await GetJsonAsync(
                $"{baseUrl}/workItems/{id}/comments?$top={20}&order=desc&api-version=7.1-preview.4", token, cancellationToken);

            return WorkItemMapper.Map(workItem.RootElement, comments?.RootElement);
        }
        catch (HttpRequestException ex)
        {
            throw new SourceUnavailableException("azure-devops", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceUnavailableException("azure-devops", ex);
        }
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
    }
}
