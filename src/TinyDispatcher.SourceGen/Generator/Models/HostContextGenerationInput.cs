namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostContextGenerationInput(
    string ContextTypeFqn,
    DiscoveryResult Discovery,
    PipelineConfig Pipeline);
