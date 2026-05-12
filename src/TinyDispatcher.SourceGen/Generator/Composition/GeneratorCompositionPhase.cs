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
        var host = _hostGenerationComposer.Compose(
            hostBootstrap,
            extraction);
        var assemblyContribution = new AssemblyContributionModel(
            extraction.ThisAssembly.Discovery,
            host.Lanes,
            hostBootstrap.IsHostProject);

        return new GeneratorModel(
            AssemblyContribution: assemblyContribution,
            References: extraction.ReferencedContributions,
            Host: host);
    }
}
