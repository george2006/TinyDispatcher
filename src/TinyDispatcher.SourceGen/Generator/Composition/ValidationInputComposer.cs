using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class ValidationInputComposer
{
    public ImmutableArray<HostContextValidationInput> Compose(
        ImmutableArray<HostContextProjection> contexts)
    {
        var validationInputs = ImmutableArray.CreateBuilder<HostContextValidationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            validationInputs.Add(BuildValidationContext(contexts[i]));
        }

        return validationInputs.ToImmutable();
    }

    private static HostContextValidationInput BuildValidationContext(
        HostContextProjection context)
    {
        return new HostContextValidationInput(
            BootstrapCalls: context.HostContext.UseTinyDispatcherCalls,
            ThisAssemblyPipeline: context.ThisAssemblyPipeline,
            GenerationInput: context.GenerationInput);
    }
}
