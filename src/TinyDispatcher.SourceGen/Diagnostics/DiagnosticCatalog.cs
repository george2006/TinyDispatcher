using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen
{
    /// <summary>
    /// Central catalog of diagnostics emitted by the TinyDispatcher source generator.
    /// </summary>
    public sealed class DiagnosticsCatalog 
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

        #region Middleware diagnostics

        public DiagnosticDescriptor InvalidMiddlewareType { get; } =
            BuildDescriptor(
                id: "DISP301",
                title: "Invalid middleware type",
                message: "Middleware '{0}' must be an open generic type definition (e.g. typeof(MyMiddleware<,>) or typeof(MyMiddleware<>)).",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor UnsupportedMiddlewareArity { get; } =
            BuildDescriptor(
                id: "DISP302",
                title: "Unsupported middleware arity",
                message: "Middleware '{0}' must have arity 1 or 2.",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor CannotResolveICommandMiddleware { get; } =
            BuildDescriptor(
                id: "DISP303",
                title: "Cannot resolve ICommandMiddleware",
                message: "Could not resolve 'TinyDispatcher.ICommandMiddleware`2' from compilation.",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor InvalidContextClosedMiddleware { get; } =
            BuildDescriptor(
                id: "DISP304",
                title: "Invalid context-closed middleware",
                message: "Middleware '{0}' must implement exactly one ICommandMiddleware<TCommand, {1}>.",
                severity: DiagnosticSeverity.Error);

        #endregion

        #region TinyBootstrap diagnostics (pipeline config)

        public DiagnosticDescriptor UnsupportedTinyBootstrapCall { get; } =
            BuildDescriptor(
                id: "DISP401",
                title: "Unsupported TinyDispatcher bootstrap call",
                message: "Unsupported TinyDispatcher bootstrap call '{0}'.",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor InvalidTinyBootstrapArguments { get; } =
            BuildDescriptor(
                id: "DISP402",
                title: "Invalid TinyDispatcher bootstrap arguments",
                message: "Invalid arguments for '{0}'. Expected: {1}.",
                severity: DiagnosticSeverity.Error);

        public DiagnosticDescriptor MiddlewareConfiguredForUnknownCommand { get; } =
            BuildDescriptor(
                id: "DISP410",
                title: "Middleware configured for unknown command",
                message: "Middleware is configured for '{0}', but no ICommandHandler was discovered for this command type in this project.",
                severity: DiagnosticSeverity.Warning);

        public DiagnosticDescriptor PolicyTargetsUnknownCommand { get; } =
            BuildDescriptor(
                id: "DISP411",
                title: "Policy targets unknown command",
                message: "Policy '{0}' targets '{1}', but no ICommandHandler was discovered for this command type in this project.",
                severity: DiagnosticSeverity.Warning);

        public DiagnosticDescriptor MultiplePoliciesForSameCommand { get; } =
            BuildDescriptor(
                id: "DISP412",
                title: "Multiple policies target the same command",
                message: "Command '{0}' is targeted by multiple policies ({1}). The first policy wins for pipeline composition.",
                severity: DiagnosticSeverity.Warning);

        public DiagnosticDescriptor TinyPolicyMissingCommandsOrMiddlewares { get; } =
            BuildDescriptor(
                id: "DISP420",
                title: "Tiny policy has no commands or middlewares",
                message: "Policy '{0}' is marked with [TinyPolicy] but does not declare both [ForCommand] and [UseMiddleware].",
                severity: DiagnosticSeverity.Warning);

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
            Diagnostic.Create(BuildDescriptor(id, title, message, DiagnosticSeverity.Error), Location.None);

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
