#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class GeneratorGenerationPhase
{
    private readonly AssemblyContributionGenerationPhase _assemblyContributionGenerationPhase = new();
    private readonly HostGenerationPhase _hostGenerationPhase = new();

    public void Generate(
        IGeneratorContext context,
        GeneratorOptions options,
        GeneratorComposition composition,
        HostBootstrapInfo hostBootstrap)
    {
        var generationPlan = BuildGenerationPlan(options, hostBootstrap, composition);

        _assemblyContributionGenerationPhase.Generate(
            context,
            generationPlan.AssemblyContribution,
            generationPlan.HostGeneration);

        _hostGenerationPhase.Generate(
            context,
            generationPlan.HostGeneration);
    }

    private static SourceGenerationPlan BuildGenerationPlan(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        GeneratorComposition composition)
    {
        var contexts = BuildPipelineGenerationPlans(options, hostBootstrap, composition.HostGeneration);
        var assemblyContribution = BuildAssemblyContributionPlan(
            options,
            composition.ThisAssemblyContributionDiscovery);
        var hostGeneration = BuildHostGenerationPlan(options, composition.HostGeneration, contexts);

        return new SourceGenerationPlan(assemblyContribution, hostGeneration);
    }

    private static PipelinePlan? BuildPipelinePlan(HostContextSourcePlan contextPlan)
    {
        if (!contextPlan.ShouldEmitPipelines)
        {
            return null;
        }

        var pipelinePlan = PipelinePlanner.Build(
            contextPlan.PipelineContributions,
            contextPlan.Discovery,
            contextPlan.EmitOptions);

        if (!pipelinePlan.ShouldEmit)
        {
            return null;
        }

        return pipelinePlan;
    }

    private static AssemblyContributionSourcePlan BuildAssemblyContributionPlan(
        GeneratorOptions options,
        DiscoveryResult assemblyContributionDiscovery)
    {
        return new AssemblyContributionSourcePlan(
            Discovery: assemblyContributionDiscovery,
            EmitOptions: BuildSharedEmitOptions(options),
            PipelineContributions: PipelineContributions.Create(PipelineConfig.Empty));
    }

    private static HostGenerationSourcePlan BuildHostGenerationPlan(
        GeneratorOptions options,
        HostGenerationComposition hostGeneration,
        ImmutableArray<HostContextSourcePlan> contexts)
    {
        return new HostGenerationSourcePlan(
            Discovery: hostGeneration.Discovery,
            EmitOptions: BuildSharedEmitOptions(options),
            Contexts: contexts);
    }

    private static ImmutableArray<HostContextSourcePlan> BuildPipelineGenerationPlans(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        HostGenerationComposition hostGeneration)
    {
        var contextInputs = hostGeneration.Contexts;
        var contextPlans = ImmutableArray.CreateBuilder<HostContextSourcePlan>(contextInputs.Length);

        for (var i = 0; i < contextInputs.Length; i++)
        {
            var contextInput = contextInputs[i].GenerationInput;

            var contextPlan = BuildContextGenerationPlan(
                BuildContextEmitOptions(options, contextInput.ContextTypeFqn),
                shouldEmitPipelines: ShouldEmitPipelines(
                    hostBootstrap,
                    contextInput.ContextTypeFqn,
                    contextInput.Pipeline),
                shouldEmitPipelineMaps: ShouldEmitPipelineMaps(hostBootstrap, contextInput.ContextTypeFqn),
                contextInput.Discovery,
                contextInput.Pipeline);

            contextPlans.Add(contextPlan with
            {
                PipelinePlan = BuildPipelinePlan(contextPlan)
            });
        }

        return contextPlans.ToImmutable();
    }

    private static HostContextSourcePlan BuildContextGenerationPlan(
        GeneratorOptions options,
        bool shouldEmitPipelines,
        bool shouldEmitPipelineMaps,
        DiscoveryResult discovery,
        PipelineConfig pipelineConfig)
    {
        var pipelineContributions = PipelineContributions.Create(pipelineConfig);

        return new HostContextSourcePlan(
            Discovery: discovery,
            EmitOptions: options,
            ShouldEmitPipelines: shouldEmitPipelines,
            ShouldEmitPipelineMaps: shouldEmitPipelineMaps,
            PipelineContributions: pipelineContributions,
            PipelinePlan: null);
    }

    private static GeneratorOptions BuildSharedEmitOptions(GeneratorOptions options)
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

