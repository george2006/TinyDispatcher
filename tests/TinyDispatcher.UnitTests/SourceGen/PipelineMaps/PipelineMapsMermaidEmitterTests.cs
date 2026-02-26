#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.PipelineMaps;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineMaps;

public sealed class PipelineMapMermaidEmitterTests
{
    [Fact]
    public void Emit_WritesFlowchartAndNodesInOrder()
    {
        var ctx = new CapturingGeneratorContext();

        var d = new PipelineDescriptor(
            CommandFullName: "global::X.Y.Cmd",
            ContextFullName: "global::X.Y.Ctx",
            HandlerFullName: "global::X.Y.Handler",
            Middlewares: new List<MiddlewareDescriptor>
            {
                new("global::X.Y.MwA", "policy"),
                new("global::X.Y.MwB", "global"),
            },
            PoliciesApplied: new List<string>());

        PipelineMapMermaidEmitter.Emit(ctx, d);

        Assert.Single(ctx.Sources);
        var (hint, text) = ctx.Sources[0];

        Assert.EndsWith(".g.mmd", hint);
        Assert.Contains("flowchart TD", text);

        // Dispatch node shortens to last segment
        Assert.Contains("A[Dispatch: Cmd]", text);

        // Edges in order
        Assert.Contains("A --> M1[policy: MwA]", text);
        Assert.Contains("M1 --> M2[global: MwB]", text);

        // Handler
        Assert.Contains("M2 --> H[Handler: Handler]", text);
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