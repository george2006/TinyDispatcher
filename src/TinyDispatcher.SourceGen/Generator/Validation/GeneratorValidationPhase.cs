#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public DiagnosticBag Validate(
        HostBootstrapInfo hostBootstrap,
        GeneratorComposition composition,
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
        GeneratorComposition composition,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var contexts = composition.ValidationContexts;
        var validationContexts = ImmutableArray.CreateBuilder<GeneratorValidationContext>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            validationContexts.Add(BuildValidationContext(
                hostBootstrap,
                composition,
                diagnosticsCatalog,
                contexts[i]));
        }

        return validationContexts.ToImmutable();
    }

    private static GeneratorValidationContext BuildValidationContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorComposition composition,
        DiagnosticsCatalog diagnosticsCatalog,
        HostContextValidationInput contextInput)
    {
        var generationInput = contextInput.GenerationInput;

        return new GeneratorValidationContext.Builder(
                generationInput.Discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(contextInput.BootstrapCalls)
            .WithContext(contextInput.ContextTypeFqn)
            .WithReferencedContributions(composition.HostGeneration.ReferencedContributions)
            .WithThisAssemblyPipelineConfig(contextInput.ThisAssemblyPipeline)
            .WithPipelineConfig(generationInput.Pipeline)
            .Build();
    }
}
