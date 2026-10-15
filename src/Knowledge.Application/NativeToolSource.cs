using Knowledge.Core;

namespace Knowledge.Application;

/// <summary>The tools implemented inside the gateway (L1 and L4).</summary>
public sealed class NativeToolSource(IEnumerable<IGatewayTool> tools) : IToolSource
{
    private readonly IReadOnlyCollection<IGatewayTool> _tools = tools.ToList();

    public IReadOnlyCollection<IGatewayTool> GetTools() => _tools;
}
