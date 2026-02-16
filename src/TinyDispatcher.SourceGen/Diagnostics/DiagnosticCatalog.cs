using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen
{
    /// <summary>
    /// Central catalog of diagnostics emitted by the TinyDispatcher source generator.
    /// </summary>
    public sealed class DiagnosticsCatalog : IDiagnostics
    {
        private const string Category = "Dispatcher";

        #region Duplicate handler diagnostics

        public DiagnosticDescriptor DuplicateCommand { get; } =
            BuildDescriptor(
                id: "DISP101",
                title: "Multiple command handlers detected",
                message: "Multiple ICommandHandler for '{0}' found: '{1}' and '{2}'",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor DuplicateQuery { get; } =
            BuildDescriptor(
                id: "DISP201",
                title: "Multiple query handlers detected",
                message: "Multiple IQueryHandler for '{0}' found: '{1}' and '{2}'",
                severity: DiagnosticSeverity.Error);

        #endregion

        #region Context diagnostics (NEW)

        public DiagnosticDescriptor MultipleContextsDetected { get; } =
            BuildDescriptor(
                id: "DISP110",
                title: "Multiple TinyDispatcher contexts detected",
                message: "Only one UseTinyDispatcher<TContext> call is allowed per project. Found {0}.",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor ContextTypeNotFound { get; } =
            BuildDescriptor(
                id: "DISP111",
                title: "TinyDispatcher context type not found",
                message: "No UseTinyDispatcher<TContext> call was found, but code generation requires a context type.",
                severity: DiagnosticSeverity.Error);

        #endregion

        #region Public API for creating diagnostics

        public Diagnostic Create(DiagnosticDescriptor descriptor, params object[] args) =>
            Diagnostic.Create(descriptor, Location.None, args);

        public Diagnostic Create(DiagnosticDescriptor descriptor, Location location, params object[] args) =>
            Diagnostic.Create(descriptor, location, args);

        public Diagnostic CreateError(string id, string title, string message) =>
            Diagnostic.Create(
                BuildDescriptor(id, title, message, DiagnosticSeverity.Error),
                Location.None);

        #endregion

        private static DiagnosticDescriptor BuildDescriptor(
            string id,
            string title,
            string message,
            DiagnosticSeverity severity)
        {
            return new DiagnosticDescriptor(
                id: id,
                title: title,
                messageFormat: message,
                category: Category,
                defaultSeverity: severity,
                isEnabledByDefault: true);
        }
    }
}
