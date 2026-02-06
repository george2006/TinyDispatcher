using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using TinyDispatcher.SourceGen.Internal;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class GeneratorOptionsFactory
{
    private readonly OptionsProvider _optionsProvider;

    public GeneratorOptionsFactory(OptionsProvider optionsProvider)
        => _optionsProvider = optionsProvider ?? throw new ArgumentNullException(nameof(optionsProvider));

    public GeneratorOptions Create(Compilation compilation, AnalyzerConfigOptionsProvider provider)
    {
        // OptionsProvider reads assembly attribute first, then build props fallback.
        var fromConfig = _optionsProvider.GetOptions(compilation, provider);

        return fromConfig ?? new GeneratorOptions(
            GeneratedNamespace: "TinyDispatcher.Generated",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: null,
            CommandContextType: null,
            EmitPipelineMap: true,
            PipelineMapFormat: "json");
    }

    public GeneratorOptions ApplyInferredContextIfMissing(GeneratorOptions baseOptions, string? inferredCtx)
    {
        if (!string.IsNullOrWhiteSpace(baseOptions.CommandContextType))
            return baseOptions;

        if (string.IsNullOrWhiteSpace(inferredCtx))
            return baseOptions;

        return new GeneratorOptions(
            GeneratedNamespace: baseOptions.GeneratedNamespace,
            EmitDiExtensions: baseOptions.EmitDiExtensions,
            EmitHandlerRegistrations: baseOptions.EmitHandlerRegistrations,
            IncludeNamespacePrefix: baseOptions.IncludeNamespacePrefix,
            CommandContextType: inferredCtx,
            EmitPipelineMap: baseOptions.EmitPipelineMap,
            PipelineMapFormat: baseOptions.PipelineMapFormat);
    }
}
