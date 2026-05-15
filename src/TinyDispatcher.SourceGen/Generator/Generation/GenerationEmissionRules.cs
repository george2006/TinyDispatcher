using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal static class GenerationEmissionRules
{
    public static bool ShouldEmitPipelines(
        bool isHostProject,
        string contextFqn,
        PipelineConfig pipeline)
    {
        if (!isHostProject)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contextFqn))
        {
            return false;
        }

        return HasAnyPipelineContributions(pipeline);
    }

    private static bool HasAnyPipelineContributions(PipelineConfig pipeline)
    {
        return pipeline.Globals.Length > 0 ||
               pipeline.PerCommand.Count > 0 ||
               pipeline.Policies.Count > 0;
    }
}
