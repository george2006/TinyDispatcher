using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class GeneratorGenerationPhase
{
    private readonly AssemblyContributionGenerationPhase _assemblyContributionGenerationPhase = new();
    private readonly HostGenerationPhase _hostGenerationPhase = new();

    public void Generate(
        IGeneratorContext context,
        GeneratorOptions options,
        GeneratorComposition composition,
        HostBootstrapInfo hostBootstrap)
    {
        var hostGenerationPlan = _hostGenerationPhase.Plan(
            options,
            hostBootstrap,
            composition.HostGeneration);
        var assemblyContributionPlan = _assemblyContributionGenerationPhase.Plan(
            options,
            composition.AssemblyContribution);

        _assemblyContributionGenerationPhase.Generate(
            context,
            assemblyContributionPlan);

        _hostGenerationPhase.Generate(
            context,
            hostGenerationPlan);
    }
}

