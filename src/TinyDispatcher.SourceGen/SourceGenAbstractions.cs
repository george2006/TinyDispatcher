#nullable enable

using System.Collections.Immutable;
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

    // ---------------------------------------------------------------------
    // Emitter abstraction (ModuleInitializerEmitter, ContributionEmitter, etc.)
    // ---------------------------------------------------------------------
    public interface ICodeEmitter
    {
        void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options);
    }

    // ---------------------------------------------------------------------
    // Handler discovery abstraction (RoslynHandlerDiscovery implements this)
    // ---------------------------------------------------------------------
    public interface IHandlerDiscovery
    {
        DiscoveryResult Discover(Compilation compilation, ImmutableArray<INamedTypeSymbol> candidates);
    }

    // ---------------------------------------------------------------------
    // Validation abstraction (DuplicateHandlerValidator implements this)
    // ---------------------------------------------------------------------
    public interface IValidator
    {
        ImmutableArray<Diagnostic> Validate(DiscoveryResult result);
    }

    // ---------------------------------------------------------------------
    // Diagnostics "catalog" abstraction (DiagnosticsCatalog implements this)
    // NOTE: Your code uses _diagnostics.DuplicateCommand / DuplicateQuery
    // ---------------------------------------------------------------------
    public interface IDiagnostics
    {
        DiagnosticDescriptor DuplicateCommand { get; }
        DiagnosticDescriptor DuplicateQuery { get; }
    }

    // ---------------------------------------------------------------------
    // Contracts produced by discovery and consumed by emitters/validators
    // ---------------------------------------------------------------------
    public sealed record HandlerContract(
        string MessageTypeFqn,
        string HandlerTypeFqn);

    public sealed record QueryHandlerContract(
        string QueryTypeFqn,
        string ResultTypeFqn,
        string HandlerTypeFqn);

    public sealed record DiscoveryResult(
        ImmutableArray<HandlerContract> Commands,
        ImmutableArray<QueryHandlerContract> Queries);
}
