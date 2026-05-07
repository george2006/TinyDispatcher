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
        var discovery = BuildDiscovery(hostBootstrap, extraction);
        var pipeline = BuildPipeline(hostBootstrap, extraction);

        return new GeneratorValidationContext.Builder(
                discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(hostBootstrap.UseTinyDispatcherCalls)
            .WithExpectedContext(hostBootstrap.ExpectedContextFqn)
            .WithReferencedContributions(extraction.ReferencedContributions)
            .WithLocalPipelineConfig(extraction.Pipeline)
            .WithPipelineConfig(pipeline)
            .Build();
    }

    private static DiscoveryResult BuildDiscovery(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        if (!ShouldMergeReferencedContributions(hostBootstrap))
            return extraction.Discovery;

        return ReferencedAssemblyContributionComposer.MergeDiscovery(
            extraction.Discovery,
            extraction.ReferencedContributions,
            hostBootstrap.ExpectedContextFqn);
    }

    private static PipelineConfig BuildPipeline(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        if (!ShouldMergeReferencedContributions(hostBootstrap))
            return extraction.Pipeline;

        return ReferencedAssemblyContributionComposer.MergePipelineConfig(
            extraction.Pipeline,
            extraction.ReferencedContributions,
            hostBootstrap.ExpectedContextFqn);
    }

    private static bool ShouldMergeReferencedContributions(HostBootstrapInfo hostBootstrap)
    {
        return hostBootstrap.IsHostProject &&
               !string.IsNullOrWhiteSpace(hostBootstrap.ExpectedContextFqn);
    }
}
