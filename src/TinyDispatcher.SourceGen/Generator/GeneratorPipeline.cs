#nullable enable

using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Analysis;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Extraction;
using TinyDispatcher.SourceGen.Generator.Models;
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
        var analysis = GeneratorAnalysisPhase.Analyze(
            input.Compilation,
            input.UseTinyCallsSyntax,
            input.Options);

        var extraction = _extractionPhase.Extract(analysis, input.HandlerSymbols);
        var validation = _validationPhase.Validate(analysis, extraction, _diagnosticsCatalog);

        if (GeneratorDiagnosticReporter.ReportAndHasErrors(context, validation.Diagnostics))
        {
            return;
        }

        _generationPhase.Generate(context, analysis.EffectiveOptions, extraction, validation);
    }
}
