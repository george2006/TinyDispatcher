#nullable enable

using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class GeneratorCompositionPhase
{
    private readonly HostGenerationComposer _hostGenerationComposer = new();
    private readonly ValidationInputComposer _validationInputComposer = new();

    public GeneratorModel Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var host = _hostGenerationComposer.Compose(
            hostBootstrap,
            extraction);
        var assemblyContribution = new AssemblyContributionModel(
            extraction.ThisAssembly.Discovery,
            host.Lanes,
            hostBootstrap.IsHostProject);
        var validationContexts = _validationInputComposer.Compose(host.Lanes);

        return new GeneratorModel(
            AssemblyContribution: assemblyContribution,
            Host: host,
            ValidationContexts: validationContexts);
    }
}
