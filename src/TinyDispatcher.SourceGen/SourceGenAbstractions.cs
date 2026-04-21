#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TinyDispatcher.SourceGen.Abstractions
{
    // ---------------------------------------------------------------------
    // Context abstraction (what RoslynGeneratorContext already implements)
    // ---------------------------------------------------------------------
    public interface IGeneratorContext
    {
        void AddSource(string hintName, SourceText sourceText);
        void ReportDiagnostic(Diagnostic diagnostic);
    }
}
