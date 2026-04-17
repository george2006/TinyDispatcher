#nullable enable

using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class GeneratorValidationPhase
{
    public GeneratorValidationResult Validate(
        GeneratorAnalysis analysis,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var validationContext = BuildValidationContext(analysis, diagnosticsCatalog);
        var diagnostics = GeneratorValidator.Validate(validationContext);

        return new GeneratorValidationResult(validationContext, diagnostics);
    }

    private static GeneratorValidationContext BuildValidationContext(
        GeneratorAnalysis analysis,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var extraction = analysis.Extraction;

        return new GeneratorValidationContext.Builder(
                analysis.Compilation,
                extraction.Discovery,
                diagnosticsCatalog)
            .WithHostGate(
                analysis.UseTinyCallsSyntax,
                isHost: analysis.UseTinyCallsSyntax.Length > 0)
            .WithUseTinyDispatcherCalls(extraction.UseTinyDispatcherCalls)
            .WithExpectedContext(GetExpectedContextFqn(analysis.EffectiveOptions))
            .WithPipelineConfig(
                extraction.Globals,
                extraction.PerCommand,
                extraction.Policies)
            .Build();
    }

    private static string GetExpectedContextFqn(GeneratorOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.CommandContextType))
        {
            return string.Empty;
        }

        return Fqn.EnsureGlobal(options.CommandContextType!);
    }
}
