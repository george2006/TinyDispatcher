#nullable enable

using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;

using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal static class PipelineMapMermaidEmitter
{
    private const string DispatchNodeName = "A";
    private const string HandlerNodeName = "H";
    private const string MiddlewareNodePrefix = "M";

    public static void Emit(IGeneratorContext context, PipelineDescriptor descriptor)
    {
        var source = WriteSource(descriptor);
        var hintName = BuildHintName(descriptor);

        context.AddSource(hintName, SourceText.From(source, Encoding.UTF8));
    }

    private static string WriteSource(PipelineDescriptor descriptor)
    {
        var writer = new CodeWriter(capacity: 4_000);

        WriteGraph(writer, descriptor);
        writer.EnsureAllBlocksClosed();

        return writer.ToString();
    }

    private static void WriteGraph(CodeWriter writer, PipelineDescriptor descriptor)
    {
        writer.Line("flowchart TD");
        WriteDispatchNode(writer, descriptor);

        var lastNode = WriteMiddlewareNodes(writer, descriptor);
        WriteHandlerNode(writer, descriptor, lastNode);
    }

    private static void WriteDispatchNode(CodeWriter writer, PipelineDescriptor descriptor)
    {
        writer.Line($"  {DispatchNodeName}[Dispatch: {Short(descriptor.CommandFullName)}]");
    }

    private static string WriteMiddlewareNodes(CodeWriter writer, PipelineDescriptor descriptor)
    {
        var lastNode = DispatchNodeName;

        for (var i = 0; i < descriptor.Middlewares.Count; i++)
        {
            lastNode = WriteMiddlewareNode(
                writer,
                previousNode: lastNode,
                middlewareNode: BuildMiddlewareNodeName(i),
                middleware: descriptor.Middlewares[i]);
        }

        return lastNode;
    }

    private static string WriteMiddlewareNode(
        CodeWriter writer,
        string previousNode,
        string middlewareNode,
        MiddlewareDescriptor middleware)
    {
        writer.Line(
            $"  {previousNode} --> {middlewareNode}[{middleware.Source}: {Short(middleware.MiddlewareFullName)}]");

        return middlewareNode;
    }

    private static string BuildMiddlewareNodeName(int middlewareIndex)
    {
        return MiddlewareNodePrefix + (middlewareIndex + 1);
    }

    private static void WriteHandlerNode(
        CodeWriter writer,
        PipelineDescriptor descriptor,
        string previousNode)
    {
        writer.Line($"  {previousNode} --> {HandlerNodeName}[Handler: {Short(descriptor.HandlerFullName)}]");
    }

    private static string BuildHintName(PipelineDescriptor descriptor)
    {
        return "PipelineMap." +
            PipelineNameFactory.SanitizeTypeName(descriptor.ContextFullName) +
            "." +
            PipelineNameFactory.SanitizeTypeName(descriptor.CommandFullName) +
            ".g.mmd";
    }

    private static string Short(string s)
    {
        var cleaned = s.StartsWith("global::", StringComparison.Ordinal)
            ? s.Substring("global::".Length)
            : s;

        var lastDot = cleaned.LastIndexOf('.');
        return lastDot >= 0
            ? cleaned.Substring(lastDot + 1)
            : cleaned;
    }

}

