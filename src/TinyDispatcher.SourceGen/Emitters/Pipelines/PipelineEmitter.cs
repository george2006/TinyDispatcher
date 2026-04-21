#nullable enable

using Microsoft.CodeAnalysis.Text;
using System.Text;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal sealed class PipelineEmitter
{
    public void Emit(IGeneratorContext context, PipelinePlan plan)
    {
        var source = PipelineSourceWriter.Write(plan);

        context.AddSource(
            hintName: "TinyDispatcherPipeline.g.cs",
            sourceText: SourceText.From(source, Encoding.UTF8));
    }
}
