#nullable enable

using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class GeneratorCompositionPhase
{
    private readonly HostGenerationComposer _hostGenerationComposer = new();

    public GeneratorModel Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var hostModel = _hostGenerationComposer.Compose(
            hostBootstrap,
            extraction);
        var assemblyContributionModel = new AssemblyContributionModel(
            extraction.ThisAssembly.Discovery,
            hostModel.Lanes,
            hostBootstrap.IsHostProject);

        return new GeneratorModel(
            AssemblyContribution: assemblyContributionModel,
            References: extraction.ReferencedContributions,
            Host: hostModel);
    }
}
