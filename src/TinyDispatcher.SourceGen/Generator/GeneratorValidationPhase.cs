#nullable enable

using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class GeneratorValidationPhase
{
    public DiagnosticBag Validate(GeneratorAnalysis analysis)
    {
        return GeneratorValidator.Validate(analysis.ValidationContext);
    }
}
