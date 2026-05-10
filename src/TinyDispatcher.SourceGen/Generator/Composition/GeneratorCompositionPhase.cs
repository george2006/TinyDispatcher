#nullable enable

using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class GeneratorCompositionPhase
{
    private readonly ContextCompositionComposer _contextCompositionComposer = new();
    private readonly HostGenerationComposer _hostGenerationComposer = new();
    private readonly ValidationInputComposer _validationInputComposer = new();

    public GeneratorComposition Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var contextCompositions = _contextCompositionComposer.Compose(
            hostBootstrap,
            extraction);
        var hostGeneration = _hostGenerationComposer.Compose(
            extraction,
            contextCompositions);
        var validationContexts = _validationInputComposer.Compose(
            contextCompositions,
            hostGeneration.Contexts);

        return new GeneratorComposition(
            AssemblyContributionDiscovery: extraction.Discovery,
            HostGeneration: hostGeneration,
            ValidationContexts: validationContexts);
    }
}
