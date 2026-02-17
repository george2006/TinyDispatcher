#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Generator.Models;

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

    // ---------------------------------------------------------------------
    // Emitter abstraction (ModuleInitializerEmitter, ContributionEmitter, etc.)
    // ---------------------------------------------------------------------
    internal interface ICodeEmitter
    {
        void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options);
    }

    // ---------------------------------------------------------------------
    // Handler discovery abstraction (RoslynHandlerDiscovery implements this)
    // ---------------------------------------------------------------------
    internal interface IHandlerDiscovery
    {
        DiscoveryResult Discover(Compilation compilation, ImmutableArray<INamedTypeSymbol> candidates);
    }
}
