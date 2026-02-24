#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal sealed class PipelineMapsEmitter : ICodeEmitter
{
    private readonly ImmutableArray<MiddlewareRef> _globals;
    private readonly ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> _perCommand;
    private readonly ImmutableDictionary<string, PolicySpec> _policies;

    public PipelineMapsEmitter(
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        _globals = globals;
        _perCommand = perCommand;
        _policies = policies;
    }

    public void Emit(IGeneratorContext context, DiscoveryResult discovery, GeneratorOptions options)
    {
        if (!options.EmitPipelineMap)
            return;

        if (string.IsNullOrWhiteSpace(options.CommandContextType))
            return;

        var formats = PipelineMapFormats.Parse(options.PipelineMapFormat);

        // if the user typed garbage, we still want predictable output (default json)
        if (!formats.EmitJson && !formats.EmitMermaid)
            formats = PipelineMapFormats.DefaultJson();

        var inspector = new PipelineMapInspector(_globals, _perCommand, _policies, options);

        EmitCommands(context, discovery.Commands, inspector, formats);
        EmitQueries(context, discovery.Queries, inspector, formats);
    }

    private static void EmitCommands(
        IGeneratorContext context,
        ImmutableArray<HandlerContract> handlers,
        PipelineMapInspector inspector,
        PipelineMapFormats formats)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            var d = inspector.InspectCommand(handlers[i]);
            EmitOne(context, d, formats);
        }
    }

    private static void EmitQueries(
        IGeneratorContext context,
        ImmutableArray<QueryHandlerContract> handlers,
        PipelineMapInspector inspector,
        PipelineMapFormats formats)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            var d = inspector.InspectQuery(handlers[i]);
            EmitOne(context, d, formats);
        }
    }

    private static void EmitOne(
        IGeneratorContext context,
        PipelineDescriptor descriptor,
        PipelineMapFormats formats)
    {
        if (formats.EmitJson)
            PipelineMapJsonEmitter.Emit(context, descriptor);

        if (formats.EmitMermaid)
            PipelineMapMermaidEmitter.Emit(context, descriptor);
    }

    private readonly struct PipelineMapFormats
    {
        public bool EmitJson { get; }
        public bool EmitMermaid { get; }

        private PipelineMapFormats(bool emitJson, bool emitMermaid)
        {
            EmitJson = emitJson;
            EmitMermaid = emitMermaid;
        }

        public static PipelineMapFormats DefaultJson()
            => new(emitJson: true, emitMermaid: false);

        public static PipelineMapFormats Parse(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return DefaultJson();

            var parts = raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            var json = false;
            var mermaid = false;

            for (var i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim().ToLowerInvariant();
                if (p == "json") json = true;
                else if (p == "mermaid") mermaid = true;
            }

            return new PipelineMapFormats(json, mermaid);
        }
    }
}