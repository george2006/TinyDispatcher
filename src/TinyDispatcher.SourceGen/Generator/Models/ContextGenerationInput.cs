namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextGenerationInput(
    string ContextTypeFqn,
    DiscoveryResult Discovery,
    PipelineConfig Pipeline);
