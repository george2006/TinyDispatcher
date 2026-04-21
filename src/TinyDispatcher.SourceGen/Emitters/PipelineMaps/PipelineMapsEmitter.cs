#nullable enable

using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal sealed class PipelineMapsEmitter
{
    public void Emit(IGeneratorContext context, PipelineMapsPlan plan)
    {
        if (!plan.ShouldEmit)
        {
            return;
        }

        for (var i = 0; i < plan.Descriptors.Length; i++)
        {
            EmitOne(context, plan.Descriptors[i], plan.Formats);
        }
    }

    private static void EmitOne(
        IGeneratorContext context,
        PipelineDescriptor descriptor,
        PipelineMapOutputFormats formats)
    {
        if (formats.EmitJson)
        {
            PipelineMapJsonEmitter.Emit(context, descriptor);
        }

        if (formats.EmitMermaid)
        {
            PipelineMapMermaidEmitter.Emit(context, descriptor);
        }
    }
}
