using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextValidationInput(
    ImmutableArray<UseTinyDispatcherCall> BootstrapCalls,
    PipelineConfig ThisAssemblyPipeline,
    ContextGenerationInput GenerationInput)
{
    public string ContextTypeFqn => GenerationInput.ContextTypeFqn;
}
