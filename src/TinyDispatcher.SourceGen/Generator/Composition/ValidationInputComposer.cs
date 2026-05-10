using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class ValidationInputComposer
{
    public ImmutableArray<ContextValidationInput> Compose(
        ImmutableArray<HostContextProjection> contexts)
    {
        var validationInputs = ImmutableArray.CreateBuilder<ContextValidationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            validationInputs.Add(BuildValidationContext(contexts[i]));
        }

        return validationInputs.ToImmutable();
    }

    private static ContextValidationInput BuildValidationContext(
        HostContextProjection context)
    {
        return new ContextValidationInput(
            BootstrapCalls: context.HostContext.UseTinyDispatcherCalls,
            ThisAssemblyPipeline: context.ThisAssemblyPipeline,
            GenerationInput: context.GenerationInput);
    }
}
