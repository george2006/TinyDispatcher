#nullable enable

using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class GeneratorCompositionPhase
{
    private readonly HostGenerationComposer _hostGenerationComposer = new();
    private readonly ValidationInputComposer _validationInputComposer = new();

    public GeneratorComposition Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var hostGeneration = _hostGenerationComposer.Compose(
            hostBootstrap,
            extraction);
        var validationContexts = _validationInputComposer.Compose(hostGeneration.Contexts);

        return new GeneratorComposition(
            ThisAssemblyContributionDiscovery: extraction.ThisAssembly.Discovery,
            HostGeneration: hostGeneration,
            ValidationContexts: validationContexts);
    }
}
