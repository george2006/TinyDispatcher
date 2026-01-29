// TinyDispatcher.SourceGen/Internal/OptionsProvider.cs

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen.Internal;

public sealed class OptionsProvider
{
    public GeneratorOptions? GetOptions(AnalyzerConfigOptionsProvider? optionsProvider)
    {
        if (optionsProvider is null)
            return null;

        var global = optionsProvider.GlobalOptions;

        string? coreNs = null;
        string? genNs = null;
        string? includePrefix = null;
        string? commandCtx = null;
        string? emitMap = null;
        string? mapFormat = null;

        global.TryGetValue("build_property.TinyDispatcher_CoreNamespace", out coreNs);
        global.TryGetValue("build_property.TinyDispatcher_GeneratedNamespace", out genNs);
        global.TryGetValue("build_property.TinyDispatcher_IncludeNamespacePrefix", out includePrefix);
        global.TryGetValue("build_property.TinyDispatcher_CommandContextType", out commandCtx);
        global.TryGetValue("build_property.TinyDispatcher_GeneratePipelineMap", out emitMap);
        global.TryGetValue("build_property.TinyDispatcher_PipelineMapFormat", out mapFormat);

        coreNs = NormalizeOptional(coreNs);
        genNs = NormalizeOptional(genNs);
        includePrefix = NormalizeOptional(includePrefix);
        commandCtx = NormalizeOptional(commandCtx);
        emitMap = NormalizeOptional(emitMap);
        mapFormat = NormalizeOptional(mapFormat);

        return new GeneratorOptions(
            CoreNamespace: coreNs ?? "TinyDispatcher",
            GeneratedNamespace: genNs ?? "TinyDispatcher.Generated",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: includePrefix,
            CommandContextType: commandCtx is null ? null : EnsureGlobal(commandCtx),

            EmitPipelineMap: string.Equals(emitMap, "true", StringComparison.OrdinalIgnoreCase),
            PipelineMapFormat: mapFormat ?? "json");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value!.Trim();

    private static string EnsureGlobal(string fqn)
    {
        if (string.IsNullOrWhiteSpace(fqn))
            throw new ArgumentException("Type name cannot be null/empty.", nameof(fqn));

        var trimmed = fqn.Trim();
        return trimmed.StartsWith("global::", StringComparison.Ordinal)
            ? trimmed
            : "global::" + trimmed;
    }
}
