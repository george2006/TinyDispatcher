namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostContextProjection(
    HostContextInfo HostContext,
    PipelineConfig ThisAssemblyPipeline,
    HostContextGenerationInput GenerationInput);
