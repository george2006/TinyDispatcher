#nullable enable

using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public GeneratorValidationResult Validate(
        GeneratorAnalysis analysis,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var validationContext = BuildValidationContext(analysis, extraction, diagnosticsCatalog);
        var diagnostics = GeneratorValidator.Validate(validationContext);

        return new GeneratorValidationResult(validationContext, diagnostics);
    }

    private static GeneratorValidationContext BuildValidationContext(
        GeneratorAnalysis analysis,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        return new GeneratorValidationContext.Builder(
                analysis.Compilation,
                extraction.Discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: analysis.HostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(analysis.HostBootstrap.UseTinyDispatcherCalls)
            .WithExpectedContext(analysis.HostBootstrap.ExpectedContextFqn)
            .WithPipelineConfig(extraction.Pipeline)
            .Build();
    }
}
