#nullable enable

using System.Text;
using Microsoft.CodeAnalysis.Text;

using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal sealed class PipelineEmitter
{
    public void Emit(IGeneratorContext context, PipelinePlan plan)
    {
        var source = PipelineSourceWriter.Write(plan);
        var hintName = GetHintName(plan);

        context.AddSource(
            hintName: hintName,
            sourceText: SourceText.From(source, Encoding.UTF8));
    }

    private static string GetHintName(PipelinePlan plan)
    {
        var hasContext = !string.IsNullOrWhiteSpace(plan.ContextFqn);
        if (!hasContext)
        {
            return "TinyDispatcherPipeline.g.cs";
        }

        return "TinyDispatcherPipeline." +
            PipelineNameFactory.SanitizeTypeName(plan.ContextFqn) +
            ".g.cs";
    }
}

