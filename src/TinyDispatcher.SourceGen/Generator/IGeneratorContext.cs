#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TinyDispatcher.SourceGen.Generator;

public interface IGeneratorContext
{
    void AddSource(string hintName, SourceText sourceText);
    void ReportDiagnostic(Diagnostic diagnostic);
}
