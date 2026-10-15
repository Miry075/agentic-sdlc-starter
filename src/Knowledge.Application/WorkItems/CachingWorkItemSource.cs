using System.Collections.Concurrent;
using Knowledge.Core;

namespace Knowledge.Application.WorkItems;

/// <summary>
/// Keeps a work item for a short time per user, so the state lookup for the policy check and the
/// read for the result make one call to the tracker. The cache is per user: it never lets one
/// person read what the tracker would refuse them.
/// </summary>
public sealed class CachingWorkItemSource(IWorkItemSource inner, TimeProvider clock, TimeSpan timeToLive) : IWorkItemSource
{
    private readonly ConcurrentDictionary<(string UserId, int Id), (DateTimeOffset ExpiresAt, WorkItem Item)> _cache = new();

    public async Task<WorkItem?> GetAsync(CallerContext caller, int id, CancellationToken cancellationToken)
    {
        var key = (caller.UserId, id);
        var now = clock.GetUtcNow();

        if (_cache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
        {
            return cached.Item;
        }

        var item = await inner.GetAsync(caller, id, cancellationToken);
        if (item is null)
        {
            _cache.TryRemove(key, out _);
            return null;
        }

        _cache[key] = (now + timeToLive, item);
        return item;
    }
}
