#nullable enable

using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal static class GeneratorDiagnosticReporter
{
    public static bool ReportAndHasErrors(IGeneratorContext context, DiagnosticBag bag)
    {
        if (bag.Count == 0)
        {
            return false;
        }

        var diagnostics = bag.ToImmutable();
        for (var i = 0; i < diagnostics.Length; i++)
        {
            context.ReportDiagnostic(diagnostics[i]);
        }

        return bag.HasErrors;
    }
}
