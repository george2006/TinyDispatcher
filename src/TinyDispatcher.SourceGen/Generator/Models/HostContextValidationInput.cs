using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record HostContextValidationInput(
    ImmutableArray<UseTinyDispatcherCall> BootstrapCalls,
    PipelineConfig ThisAssemblyPipeline,
    HostContextGenerationInput GenerationInput)
{
    public string ContextTypeFqn => GenerationInput.ContextTypeFqn;
}
