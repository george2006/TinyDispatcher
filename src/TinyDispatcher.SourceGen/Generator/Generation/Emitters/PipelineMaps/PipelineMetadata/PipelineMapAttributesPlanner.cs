using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal static class PipelineMapAttributesPlanner
{
    public static ImmutableArray<PipelineMapAttributeDescriptor> Build(PipelinePlan? pipelinePlan)
    {
        if (pipelinePlan is null)
        {
            return ImmutableArray<PipelineMapAttributeDescriptor>.Empty;
        }

        var groups = GroupCommandsByPipeline(pipelinePlan.ResolvedPipelines);
        var classNames = PipelineOrdering.GetStringsInStableOrder(groups.Keys);
        var descriptors = ImmutableArray.CreateBuilder<PipelineMapAttributeDescriptor>(classNames.Length);

        for (var i = 0; i < classNames.Length; i++)
        {
            descriptors.Add(BuildDescriptor(groups[classNames[i]], pipelinePlan));
        }

        return descriptors.ToImmutable();
    }

    private static Dictionary<string, PipelineMapAttributeGroup> GroupCommandsByPipeline(
        ImmutableArray<ResolvedPipeline> pipelines)
    {
        var groups = new Dictionary<string, PipelineMapAttributeGroup>(System.StringComparer.Ordinal);

        for (var i = 0; i < pipelines.Length; i++)
        {
            var resolvedPipeline = pipelines[i];
            var pipeline = resolvedPipeline.Pipeline;

            if (!groups.TryGetValue(pipeline.ClassName, out var group))
            {
                group = new PipelineMapAttributeGroup(pipeline);
                groups.Add(pipeline.ClassName, group);
            }

            group.CommandFullNames.Add(
                PipelineTypeNames.NormalizeFqn(resolvedPipeline.Operation.MessageTypeFqn));
        }

        return groups;
    }

    private static PipelineMapAttributeDescriptor BuildDescriptor(
        PipelineMapAttributeGroup group,
        PipelinePlan pipelinePlan)
    {
        var steps = group.Pipeline.Steps;
        var middlewares = ImmutableArray.CreateBuilder<MiddlewareRef>(steps.Length);
        var globalMiddlewareCount = 0;
        var policyMiddlewareCount = 0;

        for (var i = 0; i < steps.Length; i++)
        {
            middlewares.Add(steps[i].Middleware);

            if (steps[i].Source == PipelineStepSource.Global)
            {
                globalMiddlewareCount++;
            }
            else if (steps[i].Source == PipelineStepSource.Policy)
            {
                policyMiddlewareCount++;
            }
        }

        var commandFullNames = PipelineOrdering.GetStringsInStableOrder(group.CommandFullNames);

        return new PipelineMapAttributeDescriptor(
            PipelineTypeExpression: BuildPipelineTypeExpression(
                group.Pipeline,
                pipelinePlan.GeneratedNamespace),
            ContextFullName: pipelinePlan.ContextFqn,
            CommandFullNames: commandFullNames.ToImmutableArray(),
            Middlewares: middlewares.ToImmutable(),
            GlobalMiddlewareCount: globalMiddlewareCount,
            PolicyFullName: GetPolicyFullName(steps),
            PolicyMiddlewareCount: policyMiddlewareCount);
    }

    private static string BuildPipelineTypeExpression(
        PipelineDefinition pipeline,
        string generatedNamespace)
    {
        var pipelineType = $"global::{generatedNamespace}.{pipeline.ClassName}";
        return pipeline.IsOpenGeneric ? pipelineType + "<>" : pipelineType;
    }

    private static string? GetPolicyFullName(ImmutableArray<MiddlewareStep> steps)
    {
        string? policyFullName = null;

        for (var i = 0; i < steps.Length; i++)
        {
            if (steps[i].Source != PipelineStepSource.Policy)
            {
                continue;
            }

            var stepPolicy = steps[i].PolicyTypeFqn
                ?? throw new System.InvalidOperationException("A policy pipeline step has no policy type.");

            if (policyFullName is not null &&
                !string.Equals(policyFullName, stepPolicy, System.StringComparison.Ordinal))
            {
                throw new System.InvalidOperationException("A generated pipeline contains middleware from multiple policies.");
            }

            policyFullName = stepPolicy;
        }

        return policyFullName;
    }

    private sealed class PipelineMapAttributeGroup
    {
        public PipelineMapAttributeGroup(PipelineDefinition pipeline)
        {
            Pipeline = pipeline;
        }

        public PipelineDefinition Pipeline { get; }

        public List<string> CommandFullNames { get; } = new();
    }
}
