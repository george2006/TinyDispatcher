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
        var assemblyContribution = new AssemblyContributionComposition(
            extraction.ThisAssembly.Discovery,
            hostGeneration.Contexts,
            hostBootstrap.IsHostProject);
        var validationContexts = _validationInputComposer.Compose(hostGeneration.Contexts);

        return new GeneratorComposition(
            AssemblyContribution: assemblyContribution,
            HostGeneration: hostGeneration,
            ValidationContexts: validationContexts);
    }
}
