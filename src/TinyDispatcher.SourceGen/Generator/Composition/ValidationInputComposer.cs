using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class ValidationInputComposer
{
    public ImmutableArray<ContextValidationInput> Compose(
        ImmutableArray<ContextComposition> contexts,
        ImmutableArray<ContextGenerationInput> generationContexts)
    {
        var validationInputs = ImmutableArray.CreateBuilder<ContextValidationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            validationInputs.Add(BuildValidationContext(contexts[i], generationContexts[i]));
        }

        return validationInputs.ToImmutable();
    }

    private static ContextValidationInput BuildValidationContext(
        ContextComposition context,
        ContextGenerationInput generationContext)
    {
        return new ContextValidationInput(
            BootstrapCalls: context.HostContext.UseTinyDispatcherCalls,
            ThisAssemblyPipeline: context.ThisAssemblyPipeline,
            GenerationInput: generationContext);
    }
}
