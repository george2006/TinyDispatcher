namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextGenerationInput(
    string ContextTypeFqn,
    DiscoveryResult LocalDiscovery,
    DiscoveryResult Discovery,
    PipelineConfig LocalPipeline,
    PipelineConfig Pipeline);
