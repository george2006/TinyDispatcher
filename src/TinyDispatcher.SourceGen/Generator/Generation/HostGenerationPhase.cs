using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class HostGenerationPhase
{
    public void Generate(
        IGeneratorContext context,
        HostGenerationSourcePlan hostGeneration)
    {
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            hostGeneration.Discovery,
            hostGeneration.EmitOptions,
            hasPipelineContributions: HasPipelinePlans(hostGeneration.Contexts));

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);

        EmitPipelineSources(context, hostGeneration);
        EmitPipelineMaps(context, hostGeneration);
    }

    private static void EmitPipelineSources(
        IGeneratorContext context,
        HostGenerationSourcePlan hostGeneration)
    {
        for (var i = 0; i < hostGeneration.Contexts.Length; i++)
        {
            var pipelinePlan = hostGeneration.Contexts[i].PipelinePlan;
            if (pipelinePlan is null)
            {
                continue;
            }

            new PipelineEmitter().Emit(context, pipelinePlan);
        }
    }

    private static void EmitPipelineMaps(
        IGeneratorContext context,
        HostGenerationSourcePlan hostGeneration)
    {
        for (var i = 0; i < hostGeneration.Contexts.Length; i++)
        {
            var contextPlan = hostGeneration.Contexts[i];
            if (!contextPlan.ShouldEmitPipelineMaps)
            {
                continue;
            }

            var pipelineMapsPlan = PipelineMapsPlanner.Build(
                contextPlan.Discovery,
                contextPlan.PipelineContributions,
                contextPlan.EmitOptions);

            new PipelineMapsEmitter().Emit(context, pipelineMapsPlan);
        }
    }

    private static bool HasPipelinePlans(ImmutableArray<HostContextSourcePlan> contextPlans)
    {
        for (var i = 0; i < contextPlans.Length; i++)
        {
            if (contextPlans[i].PipelinePlan is not null)
            {
                return true;
            }
        }

        return false;
    }
}
