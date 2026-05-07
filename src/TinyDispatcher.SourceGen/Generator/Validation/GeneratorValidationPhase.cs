#nullable enable

using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public GeneratorValidationResult Validate(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog,
        ValidationRoslynDependencies roslynDependencies)
    {
        var validationContext = BuildValidationContext(
            hostBootstrap,
            extraction,
            diagnosticsCatalog);
        var diagnostics = GeneratorValidator.Validate(
            validationContext,
            roslynDependencies.CommandMiddlewareInterface,
            roslynDependencies.MiddlewareTypeResolver);

        return new GeneratorValidationResult(validationContext, diagnostics);
    }

    private static GeneratorValidationContext BuildValidationContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var discovery = BuildDiscovery(extraction, hostBootstrap.ExpectedContextFqn);
        var pipeline = BuildPipeline(extraction, hostBootstrap.ExpectedContextFqn);

        return new GeneratorValidationContext.Builder(
                discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(hostBootstrap.UseTinyDispatcherCalls)
            .WithExpectedContext(hostBootstrap.ExpectedContextFqn)
            .WithReferencedContributions(extraction.ReferencedContributions)
            .WithPipelineConfig(pipeline)
            .Build();
    }

    private static DiscoveryResult BuildDiscovery(GeneratorExtraction extraction, string expectedContextFqn)
    {
        return ReferencedAssemblyContributionComposer.MergeDiscovery(
            extraction.Discovery,
            extraction.ReferencedContributions,
            expectedContextFqn);
    }

    private static PipelineConfig BuildPipeline(GeneratorExtraction extraction, string expectedContextFqn)
    {
        return ReferencedAssemblyContributionComposer.MergePipelineConfig(
            extraction.Pipeline,
            extraction.ReferencedContributions,
            expectedContextFqn);
    }
}
