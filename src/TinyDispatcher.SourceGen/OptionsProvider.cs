// TinyDispatcher.SourceGen/Internal/OptionsProvider.cs
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen.Internal;

public sealed class OptionsProvider
{
    public GeneratorOptions? GetOptions(Compilation compilation, AnalyzerConfigOptionsProvider? optionsProvider)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));

        // 1) Assembly attribute wins
        var fromAttr = TryReadFromAssemblyAttribute(compilation);
        if (fromAttr is not null)
            return fromAttr;

        // 2) Fallback to build props (back-compat)
        if (optionsProvider is null)
            return null;

        var global = optionsProvider.GlobalOptions;

        global.TryGetValue("build_property.TinyDispatcher_GeneratedNamespace", out var genNs);
        global.TryGetValue("build_property.TinyDispatcher_IncludeNamespacePrefix", out var includePrefix);
        global.TryGetValue("build_property.TinyDispatcher_CommandContextType", out var commandCtx);
        global.TryGetValue("build_property.TinyDispatcher_GeneratePipelineMap", out var emitMap);
        global.TryGetValue("build_property.TinyDispatcher_PipelineMapFormat", out var mapFormat);

        genNs = NormalizeOptional(genNs);
        includePrefix = NormalizeOptional(includePrefix);
        commandCtx = NormalizeOptional(commandCtx);
        emitMap = NormalizeOptional(emitMap);
        mapFormat = NormalizeOptional(mapFormat);

        return new GeneratorOptions(
            GeneratedNamespace: genNs ?? "TinyDispatcher.Generated",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: includePrefix,
            CommandContextType: commandCtx is null ? null : EnsureGlobal(commandCtx),
            EmitPipelineMap: string.Equals(emitMap, "true", StringComparison.OrdinalIgnoreCase),
            PipelineMapFormat: mapFormat ?? "json");
    }

    private static GeneratorOptions? TryReadFromAssemblyAttribute(Compilation compilation)
    {
        // Attribute type lives in runtime: TinyDispatcher.TinyDispatcherGeneratorOptionsAttribute
        var attrType = compilation.GetTypeByMetadataName("TinyDispatcher.TinyDispatcherGeneratorOptionsAttribute");
        if (attrType is null)
            return null;

        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType))
                continue;

            string? genNs = GetNamedString(attr, "GeneratedNamespace");
            string? includePrefix = GetNamedString(attr, "IncludeNamespacePrefix");

            var ctxType = GetNamedType(attr, "CommandContextType"); // Type?
            var ctxFqn = ctxType is null ? null : EnsureGlobal(ctxType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            var emitMap = GetNamedBool(attr, "EmitPipelineMap") ?? false;
            var mapFormat = GetNamedString(attr, "PipelineMapFormat") ?? "json";

            genNs = NormalizeOptional(genNs);
            includePrefix = NormalizeOptional(includePrefix);

            return new GeneratorOptions(
                GeneratedNamespace: genNs ?? "TinyDispatcher.Generated",
                EmitDiExtensions: true,
                EmitHandlerRegistrations: true,
                IncludeNamespacePrefix: includePrefix,
                CommandContextType: ctxFqn,
                EmitPipelineMap: emitMap,
                PipelineMapFormat: mapFormat);
        }

        return null;
    }

    private static string? GetNamedString(AttributeData attr, string name)
    {
        foreach (var kv in attr.NamedArguments)
            if (kv.Key == name && kv.Value.Value is string s)
                return s;
        return null;
    }

    private static bool? GetNamedBool(AttributeData attr, string name)
    {
        foreach (var kv in attr.NamedArguments)
            if (kv.Key == name && kv.Value.Value is bool b)
                return b;
        return null;
    }

    private static ITypeSymbol? GetNamedType(AttributeData attr, string name)
    {
        foreach (var kv in attr.NamedArguments)
        {
            if (kv.Key != name) continue;
            if (kv.Value.Kind == TypedConstantKind.Type && kv.Value.Value is ITypeSymbol ts)
                return ts;
        }
        return null;
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
