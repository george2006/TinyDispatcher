#nullable enable

using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Extraction;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using TinyDispatcher.SourceGen.Generator.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class GeneratorPipeline
{
    private readonly DiagnosticsCatalog _diagnosticsCatalog = new();
    private readonly GeneratorExtractionPhase _extractionPhase = new();
    private readonly GeneratorValidationPhase _validationPhase = new();
    private readonly GeneratorGenerationPhase _generationPhase = new();

    public void Execute(IGeneratorContext context, GeneratorInput input)
    {
        var analysisResult = GeneratorAnalysisPhase.Analyze(
            input.Compilation,
            input.UseTinyCallsSyntax,
            input.Options);
        var analysis = analysisResult.Analysis;
        var validationDependencies = ValidationRoslynDependencies.Create(input.Compilation);

        var extraction = _extractionPhase.Extract(
            input.Compilation,
            input.HandlerSymbols,
            analysisResult.ConfirmedBootstrapLambdas,
            analysis.EffectiveOptions);
        var validation = _validationPhase.Validate(
            analysis.HostBootstrap,
            extraction,
            _diagnosticsCatalog,
            validationDependencies);

        if (GeneratorDiagnosticReporter.ReportAndHasErrors(context, validation.Diagnostics))
        {
            return;
        }

        _generationPhase.Generate(context, analysis.EffectiveOptions, extraction, validation);
    }
}
