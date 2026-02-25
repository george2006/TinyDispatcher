#nullable enable

using System;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal static class PipelineMapMermaidEmitter
{
    public static void Emit(IGeneratorContext context, PipelineDescriptor d)
    {
        var w = new CodeWriter(capacity: 4_000);

        w.Line("flowchart TD");
        w.Line($"  A[Dispatch: {Short(d.CommandFullName)}]");

        var prev = "A";
        var idx = 1;

        foreach (var mw in d.Middlewares)
        {
            var node = $"M{idx++}";
            w.Line($"  {prev} --> {node}[{mw.Source}: {Short(mw.MiddlewareFullName)}]");
            prev = node;
        }

        w.Line($"  {prev} --> H[Handler: {Short(d.HandlerFullName)}]");

        w.EnsureAllBlocksClosed();

        var hint = $"PipelineMap.{Sanitize(d.CommandFullName)}.g.mmd";
        context.AddSource(hint, SourceText.From(w.ToString(), Encoding.UTF8));
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

    private static string Sanitize(string s)
        => s.Replace("global::", "")
            .Replace('.', '_')
            .Replace('+', '_');
}