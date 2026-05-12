#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public DiagnosticBag Validate(
        HostBootstrapInfo hostBootstrap,
        GeneratorModel composition,
        DiagnosticsCatalog diagnosticsCatalog,
        ValidationRoslynDependencies roslynDependencies)
    {
        var validationContexts = BuildValidationContexts(
            hostBootstrap,
            composition,
            diagnosticsCatalog);
        var diagnostics = new DiagnosticBag();

        for (var i = 0; i < validationContexts.Length; i++)
        {
            var contextDiagnostics = GeneratorValidator.Validate(
                validationContexts[i],
                roslynDependencies.CommandMiddlewareInterface,
                roslynDependencies.MiddlewareTypeResolver);

            diagnostics.AddRange(contextDiagnostics.ToImmutable());
        }

        return diagnostics;
    }

    private static ImmutableArray<GeneratorValidationContext> BuildValidationContexts(
        HostBootstrapInfo hostBootstrap,
        GeneratorModel composition,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var lanes = composition.Host.Lanes;
        var validationContexts = ImmutableArray.CreateBuilder<GeneratorValidationContext>(lanes.Length);

        for (var i = 0; i < lanes.Length; i++)
        {
            validationContexts.Add(BuildValidationContext(
                hostBootstrap,
                composition,
                diagnosticsCatalog,
                lanes[i]));
        }

        return validationContexts.ToImmutable();
    }

    private static GeneratorValidationContext BuildValidationContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorModel composition,
        DiagnosticsCatalog diagnosticsCatalog,
        HostLane lane)
    {
        var generationInput = lane.GenerationInput;

        return new GeneratorValidationContext.Builder(
                generationInput.Discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(lane.Declaration.UseTinyDispatcherCalls)
            .WithContext(generationInput.ContextTypeFqn)
            .WithReferencedContributions(composition.Host.ReferencedContributions)
            .WithThisAssemblyPipelineConfig(lane.ThisAssemblyPipeline)
            .WithPipelineConfig(generationInput.Pipeline)
            .Build();
    }
}
