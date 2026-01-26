using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Abstractions;

internal sealed class RoslynGeneratorContext : IGeneratorContext
{
    private readonly SourceProductionContext _spc;

    public RoslynGeneratorContext(SourceProductionContext spc)
    {
        _spc = spc;
    }

    public void AddSource(string hintName, SourceText sourceText) =>
        _spc.AddSource(hintName, sourceText);

    public void ReportDiagnostic(Diagnostic diagnostic) =>
        _spc.ReportDiagnostic(diagnostic);
}