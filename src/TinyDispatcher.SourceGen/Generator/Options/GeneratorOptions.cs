namespace TinyDispatcher.SourceGen.Generator.Options
{
    /// <summary>
    /// Immutable configuration for the TinyDispatcher source generator.
    /// Values come from MSBuild / .editorconfig.
    /// </summary>
    public sealed record GeneratorOptions(
        string GeneratedNamespace,
        bool EmitDiExtensions,
        bool EmitHandlerRegistrations,
        string? IncludeNamespacePrefix,
        string? CommandContextType,
        bool EmitPipelineMap,
        string? PipelineMapFormat
    );

    internal static class Known
    {
        internal const string CoreNamespace = "TinyDispatcher";
    }
}
