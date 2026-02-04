// TinyDispatcher/GeneratorOptionsAttribute.cs
#nullable enable
using System;

namespace TinyDispatcher;

/// <summary>
/// Assembly-level configuration for TinyDispatcher source generation.
/// Prefer this over MSBuild properties.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class TinyDispatcherGeneratorOptionsAttribute : Attribute
{
    // Namespaces
    public string? GeneratedNamespace { get; set; }
    public string? IncludeNamespacePrefix { get; set; }

    // Context
    public Type? CommandContextType { get; set; }

    // Optional extras (if you keep these features)
    public bool EmitPipelineMap { get; set; }
    public string? PipelineMapFormat { get; set; } // "json", etc.
}
