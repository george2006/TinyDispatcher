#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.PipelineMaps;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineMaps;

public sealed class PipelineMapJsonEmitterTests
{
    [Fact]
    public void Emit_WritesExpectedHeader_AndEscapesJson()
    {
        var ctx = new CapturingGeneratorContext();

        var d = new PipelineDescriptor(
            CommandFullName: "global::A.B.MyCommand",
            ContextFullName: "global::A.B.MyContext",
            HandlerFullName: "global::A.B.MyHandler",
            Middlewares: new List<MiddlewareDescriptor>
            {
                new("global::A.B.Mw1", "policy"),
                new("global::A.B.Mw2", "global"),
            },
            PoliciesApplied: new List<string>
            {
                "Policy\"X",
                "Policy\\Y"
            });

        PipelineMapJsonEmitter.Emit(ctx, d);

        Assert.Single(ctx.Sources);
        var (hint, text) = ctx.Sources[0];

        Assert.EndsWith(".g.cs", hint);
        Assert.Contains("TINYDISPATCHER_PIPELINE_MAP_JSON", text);

        // Escaping checks
        Assert.Contains("\\\"", text);  // quote escaped
        Assert.Contains("\\\\", text);  // backslash escaped

        // Key fields present (and global:: stripped only by JSON value formatting? your emitter uses Escape only)
        Assert.Contains("\"command\": \"global::A.B.MyCommand\"", text);
        Assert.Contains("\"context\": \"global::A.B.MyContext\"", text);
        Assert.Contains("\"handler\": \"global::A.B.MyHandler\"", text);

        // Middleware entries present
        Assert.Contains("\"type\": \"global::A.B.Mw1\"", text);
        Assert.Contains("\"source\": \"policy\"", text);
        Assert.Contains("\"type\": \"global::A.B.Mw2\"", text);
        Assert.Contains("\"source\": \"global\"", text);
    }

    private sealed class CapturingGeneratorContext : IGeneratorContext
    {
        public List<(string HintName, string Content)> Sources { get; } = new();

        public void AddSource(string hintName, SourceText sourceText)
            => Sources.Add((hintName, sourceText.ToString()));

        public void ReportDiagnostic(Diagnostic diagnostic)
        {
            throw new NotImplementedException();
        }
    }
}