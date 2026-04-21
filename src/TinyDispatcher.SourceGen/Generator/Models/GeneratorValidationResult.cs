using TinyDispatcher.SourceGen.Generator.Validation;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorValidationResult(
    GeneratorValidationContext Context,
    DiagnosticBag Diagnostics);
