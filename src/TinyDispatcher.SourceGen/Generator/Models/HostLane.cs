namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostLane(
    HostLaneDeclaration Declaration,
    PipelineConfig ThisAssemblyPipeline,
    HostContextGenerationInput GenerationInput);
