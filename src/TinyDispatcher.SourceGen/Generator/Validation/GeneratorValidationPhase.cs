#nullable enable

using TinyDispatcher.SourceGen.Diagnostics;
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
        return new GeneratorValidationContext.Builder(
                extraction.Discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(hostBootstrap.UseTinyDispatcherCalls)
            .WithExpectedContext(hostBootstrap.ExpectedContextFqn)
            .WithPipelineConfig(extraction.Pipeline)
            .Build();
    }
}
