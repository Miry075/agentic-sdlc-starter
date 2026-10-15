using System.Text.Json;
using Knowledge.Core;

namespace Knowledge.Adapters.Dev;

/// <summary>Work items stored as <c>&lt;id&gt;.json</c> files. Dev mode only: it lets anyone try the gateway without Azure DevOps.</summary>
public sealed class FileWorkItemSource(string directory) : IWorkItemSource
{
    public async Task<WorkItem?> GetAsync(CallerContext caller, int id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, $"{id}.json");
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var item = await JsonSerializer.DeserializeAsync<WorkItem>(stream, JsonSerializerOptions.Web, cancellationToken);

        return item is null ? null : item with
        {
            Comments = item.Comments ?? [],
            Links = item.Links ?? [],
        };
    }
}
