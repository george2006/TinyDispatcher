#nullable enable

using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal static class GeneratorValidator
{
    private static readonly ContextConsistencyValidator _contextConsistency = new();
    private static readonly DuplicateHandlerValidator _duplicateHandler = new();
    private static readonly MissingHandlerValidator _missingHandler = new();
    private static readonly MiddlewareRefShapeValidator _middlewareRefShape = new();
    private static readonly PipelineDiagnosticsValidator _pipelineDiagnostics = new();

    public static DiagnosticBag Validate(GeneratorValidationContext vctx)
    {
        var bag = new DiagnosticBag();

        _contextConsistency.Validate(vctx, bag);
        _duplicateHandler.Validate(vctx, bag);
        _missingHandler.Validate(vctx, bag);
        _middlewareRefShape.Validate(vctx, bag);
        _pipelineDiagnostics.Validate(vctx, bag);

        return bag;
    }
}