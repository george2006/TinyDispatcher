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

        /// <summary>
        /// Emitted when more than one ICommandHandler&lt;T&gt; is discovered for the same command.
        /// </summary>
        public DiagnosticDescriptor DuplicateCommand { get; } =
            BuildDescriptor(
                id: "DISP101",
                title: "Multiple command handlers detected",
                message: "Multiple ICommandHandler for '{0}' found: '{1}' and '{2}'",
                severity: DiagnosticSeverity.Error);

        /// <summary>
        /// Emitted when more than one IQueryHandler&lt;TQuery,TResult&gt; is discovered for the same query.
        /// </summary>
        public DiagnosticDescriptor DuplicateQuery { get; } =
            BuildDescriptor(
                id: "DISP201",
                title: "Multiple query handlers detected",
                message: "Multiple IQueryHandler for '{0}' found: '{1}' and '{2}'",
                severity: DiagnosticSeverity.Error);

        #endregion

        #region Public API for creating diagnostics

        public Diagnostic Create(DiagnosticDescriptor descriptor, params object[] args) =>
            Diagnostic.Create(descriptor, Location.None, args);

        public Diagnostic CreateError(string id, string title, string message) =>
            Diagnostic.Create(
                BuildDescriptor(id, title, message, DiagnosticSeverity.Error),
                Location.None);

        #endregion

        #region Internal helpers

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

        #endregion
    }
}
