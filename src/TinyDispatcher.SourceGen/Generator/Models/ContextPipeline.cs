namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextPipeline(
    string ContextTypeFqn,
    PipelineConfig Pipeline);
