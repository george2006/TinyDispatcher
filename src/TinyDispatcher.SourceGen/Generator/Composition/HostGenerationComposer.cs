using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class HostGenerationComposer
{
    public HostGenerationComposition Compose(
        GeneratorExtraction extraction,
        ImmutableArray<ContextComposition> contexts)
    {
        var generationContexts = BuildGenerationContexts(contexts);
        var discovery = BuildHostDiscovery(extraction, generationContexts);

        return new HostGenerationComposition(
            discovery,
            extraction.ReferencedContributions,
            generationContexts);
    }

    private static ImmutableArray<ContextGenerationInput> BuildGenerationContexts(
        ImmutableArray<ContextComposition> contexts)
    {
        var generationInputs = ImmutableArray.CreateBuilder<ContextGenerationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            generationInputs.Add(BuildGenerationContext(contexts[i]));
        }

        return generationInputs.ToImmutable();
    }

    private static ContextGenerationInput BuildGenerationContext(
        ContextComposition context)
    {
        return new ContextGenerationInput(
            ContextTypeFqn: context.ContextTypeFqn,
            Discovery: context.HostDiscovery,
            Pipeline: context.HostPipeline);
    }

    private static DiscoveryResult BuildHostDiscovery(
        GeneratorExtraction extraction,
        ImmutableArray<ContextGenerationInput> contexts)
    {
        if (contexts.IsDefaultOrEmpty)
        {
            return extraction.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < contexts.Length; i++)
        {
            commands.AddRange(contexts[i].Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            extraction.Discovery.Queries);
    }
}
