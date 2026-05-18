using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostLane(
    HostLaneDeclaration Declaration,
    PipelineConfig ThisAssemblyPipeline,
    DiscoveryResult Discovery,
    PipelineConfig Pipeline)
{
    public string ContextTypeFqn => Declaration.ContextTypeFqn;

    public ImmutableArray<UseTinyDispatcherCall> BootstrapCalls => Declaration.UseTinyDispatcherCalls;
}
