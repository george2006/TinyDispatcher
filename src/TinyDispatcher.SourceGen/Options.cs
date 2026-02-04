namespace TinyDispatcher.SourceGen
{
    /// <summary>
    /// Immutable configuration for the TinyDispatcher source generator.
    /// Values come from MSBuild / .editorconfig.
    /// </summary>
    public sealed record GeneratorOptions(
        string GeneratedNamespace,       // e.g. "TinyDispatcher.Generated"
        bool EmitDiExtensions,           // emit DI extensions or not
        bool EmitHandlerRegistrations,   // emit AddDispatcherHandlers or not
        string? IncludeNamespacePrefix,  // optional filter for handler namespaces

        // NEW: Closed command context type (fully-qualified, can be "global::X.Y.Z")
        string? CommandContextType,
        // NEW: pipeline map emission
        bool EmitPipelineMap,
        string? PipelineMapFormat
    );

    internal static class Known
    {
        internal const string CoreNamespace = "TinyDispatcher";
    }
}