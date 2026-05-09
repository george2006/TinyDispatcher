namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextPipelineConfig(
    string ContextTypeFqn,
    PipelineConfig Pipeline);
