using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class HostGenerationPhase
{
    public HostGenerationSourcePlan Plan(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        HostModel hostGeneration)
    {
        var lanes = BuildLanePlans(options, hostBootstrap, hostGeneration);

        return new HostGenerationSourcePlan(
            Discovery: hostGeneration.Discovery,
            EmitOptions: BuildEmitOptions(options),
            Lanes: lanes);
    }

    public void Generate(
        IGeneratorContext context,
        HostGenerationSourcePlan hostGeneration)
    {
        EmitPipelineSources(context, hostGeneration);
        EmitPipelineMaps(context, hostGeneration);
    }

    private static void EmitPipelineSources(
        IGeneratorContext context,
        HostGenerationSourcePlan hostGeneration)
    {
        for (var i = 0; i < hostGeneration.Lanes.Length; i++)
        {
            var pipelinePlan = hostGeneration.Lanes[i].PipelinePlan;
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
        for (var i = 0; i < hostGeneration.Lanes.Length; i++)
        {
            var lanePlan = hostGeneration.Lanes[i];
            if (!lanePlan.ShouldEmitPipelineMaps)
            {
                continue;
            }

            var pipelineMapsPlan = PipelineMapsPlanner.Build(
                lanePlan.Discovery,
                lanePlan.PipelineContributions,
                lanePlan.EmitOptions);

            new PipelineMapsEmitter().Emit(context, pipelineMapsPlan);
        }
    }

    private static ImmutableArray<HostLaneSourcePlan> BuildLanePlans(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        HostModel hostGeneration)
    {
        var lanes = hostGeneration.Lanes;
        var lanePlans = ImmutableArray.CreateBuilder<HostLaneSourcePlan>(lanes.Length);

        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];

            var lanePlan = BuildLanePlan(
                BuildContextEmitOptions(options, lane.ContextTypeFqn),
                shouldEmitPipelines: ShouldEmitPipelines(
                    hostBootstrap,
                    lane.ContextTypeFqn,
                    lane.Pipeline),
                shouldEmitPipelineMaps: ShouldEmitPipelineMaps(hostBootstrap, lane.ContextTypeFqn),
                lane.Discovery,
                lane.Pipeline);

            lanePlans.Add(lanePlan with
            {
                PipelinePlan = BuildPipelinePlan(lanePlan)
            });
        }

        return lanePlans.ToImmutable();
    }

    private static HostLaneSourcePlan BuildLanePlan(
        GeneratorOptions options,
        bool shouldEmitPipelines,
        bool shouldEmitPipelineMaps,
        DiscoveryResult discovery,
        PipelineConfig pipelineConfig)
    {
        var pipelineContributions = PipelineContributions.Create(pipelineConfig);

        return new HostLaneSourcePlan(
            Discovery: discovery,
            EmitOptions: options,
            ShouldEmitPipelines: shouldEmitPipelines,
            ShouldEmitPipelineMaps: shouldEmitPipelineMaps,
            PipelineContributions: pipelineContributions,
            PipelinePlan: null);
    }

    private static PipelinePlan? BuildPipelinePlan(HostLaneSourcePlan lanePlan)
    {
        if (!lanePlan.ShouldEmitPipelines)
        {
            return null;
        }

        var pipelinePlan = PipelinePlanner.Build(
            lanePlan.PipelineContributions,
            lanePlan.Discovery,
            lanePlan.EmitOptions);

        if (!pipelinePlan.ShouldEmit)
        {
            return null;
        }

        return pipelinePlan;
    }

    private static GeneratorOptions BuildEmitOptions(GeneratorOptions options)
    {
        return new GeneratorOptions(
            GeneratedNamespace: options.GeneratedNamespace,
            EmitDiExtensions: options.EmitDiExtensions,
            EmitHandlerRegistrations: options.EmitHandlerRegistrations,
            IncludeNamespacePrefix: options.IncludeNamespacePrefix,
            CommandContextType: null,
            EmitPipelineMap: options.EmitPipelineMap,
            PipelineMapFormat: options.PipelineMapFormat);
    }

    private static GeneratorOptions BuildContextEmitOptions(
        GeneratorOptions options,
        string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(contextFqn))
        {
            return options;
        }

        return new GeneratorOptions(
            GeneratedNamespace: options.GeneratedNamespace,
            EmitDiExtensions: options.EmitDiExtensions,
            EmitHandlerRegistrations: options.EmitHandlerRegistrations,
            IncludeNamespacePrefix: options.IncludeNamespacePrefix,
            CommandContextType: contextFqn,
            EmitPipelineMap: options.EmitPipelineMap,
            PipelineMapFormat: options.PipelineMapFormat);
    }

    private static bool ShouldEmitPipelines(
        HostBootstrapInfo hostBootstrap,
        string contextFqn,
        PipelineConfig pipeline)
    {
        if (!hostBootstrap.IsHostProject)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contextFqn))
        {
            return false;
        }

        return HasAnyPipelineContributions(pipeline);
    }

    private static bool ShouldEmitPipelineMaps(
        HostBootstrapInfo hostBootstrap,
        string contextFqn)
    {
        return hostBootstrap.IsHostProject &&
               !string.IsNullOrWhiteSpace(contextFqn);
    }

    private static bool HasAnyPipelineContributions(PipelineConfig pipeline)
    {
        return pipeline.Globals.Length > 0 ||
               pipeline.PerCommand.Count > 0 ||
               pipeline.Policies.Count > 0;
    }

}
