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
        GeneratorModel composition,
        HostBootstrapInfo hostBootstrap)
    {
        var assemblyContributionPlan = _assemblyContributionGenerationPhase.Plan(
            options,
            composition.AssemblyContribution);

        _assemblyContributionGenerationPhase.Generate(
            context,
            assemblyContributionPlan);

        var hostGenerationPlan = _hostGenerationPhase.Plan(
            options,
            hostBootstrap,
            composition.Host);

        _hostGenerationPhase.Generate(
            context,
            hostGenerationPlan);
    }
}

