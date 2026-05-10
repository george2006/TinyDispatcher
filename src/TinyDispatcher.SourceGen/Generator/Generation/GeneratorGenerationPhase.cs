#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class GeneratorGenerationPhase
{
    public void Generate(
        IGeneratorContext context,
        GeneratorOptions options,
        GeneratorComposition composition,
        HostBootstrapInfo hostBootstrap)
    {
        var generationPlan = BuildGenerationPlan(options, hostBootstrap, composition);

        EmitAssemblyContributionSources(context, generationPlan);
        EmitHostGenerationSources(context, generationPlan);
    }

    private static void EmitAssemblyContributionSources(
        IGeneratorContext context,
        SourceGenerationPlan generationPlan)
    {
        var assemblyContribution = generationPlan.AssemblyContribution;
        new EmptyPipelineContributionEmitter().Emit(
            context,
            assemblyContribution.Discovery,
            assemblyContribution.PipelineContributions,
            assemblyContribution.EmitOptions,
            GetPipelineRegistrationMethodNames(generationPlan.HostGeneration.Contexts),
            GetPipelineContributionSources(generationPlan.HostGeneration.Contexts));

        var handlerRegistrationsPlan = HandlerRegistrationsPlanner.Build(
            assemblyContribution.Discovery,
            assemblyContribution.EmitOptions);

        new HandlerRegistrationsEmitter().Emit(context, handlerRegistrationsPlan);
    }

    private static void EmitHostGenerationSources(
        IGeneratorContext context,
        SourceGenerationPlan generationPlan)
    {
        var hostGeneration = generationPlan.HostGeneration;
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

    private static PipelinePlan? BuildPipelinePlan(ContextSourcePlan contextPlan)
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
        ImmutableArray<ContextSourcePlan> contexts)
    {
        return new HostGenerationSourcePlan(
            Discovery: hostGeneration.Discovery,
            EmitOptions: BuildSharedEmitOptions(options),
            Contexts: contexts);
    }

    private static ImmutableArray<ContextSourcePlan> BuildPipelineGenerationPlans(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        HostGenerationComposition hostGeneration)
    {
        var contextInputs = hostGeneration.Contexts;
        var contextPlans = ImmutableArray.CreateBuilder<ContextSourcePlan>(contextInputs.Length);

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

    private static ContextSourcePlan BuildContextGenerationPlan(
        GeneratorOptions options,
        bool shouldEmitPipelines,
        bool shouldEmitPipelineMaps,
        DiscoveryResult discovery,
        PipelineConfig pipelineConfig)
    {
        var pipelineContributions = PipelineContributions.Create(pipelineConfig);

        return new ContextSourcePlan(
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

    private static ImmutableArray<string> GetPipelineRegistrationMethodNames(
        ImmutableArray<ContextSourcePlan> contextPlans)
    {
        if (contextPlans.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var methodNames = ImmutableArray.CreateBuilder<string>(contextPlans.Length);

        for (var i = 0; i < contextPlans.Length; i++)
        {
            var pipelinePlan = contextPlans[i].PipelinePlan;
            if (pipelinePlan is null)
            {
                continue;
            }

            methodNames.Add(PipelineNameFactory.PipelineRegistrationMethodName(
                pipelinePlan.ContextFqn));
        }

        return methodNames.ToImmutable();
    }

    private static ImmutableArray<EmptyPipelineContributionEmitter.PipelineContributionSource> GetPipelineContributionSources(
        ImmutableArray<ContextSourcePlan> contextPlans)
    {
        if (contextPlans.IsDefaultOrEmpty)
        {
            return ImmutableArray<EmptyPipelineContributionEmitter.PipelineContributionSource>.Empty;
        }

        var sources = ImmutableArray.CreateBuilder<EmptyPipelineContributionEmitter.PipelineContributionSource>(contextPlans.Length);

        for (var i = 0; i < contextPlans.Length; i++)
        {
            var contextPlan = contextPlans[i];
            sources.Add(new EmptyPipelineContributionEmitter.PipelineContributionSource(
                contextPlan.EmitOptions,
                contextPlan.PipelineContributions));
        }

        return sources.ToImmutable();
    }

    private static bool HasPipelinePlans(ImmutableArray<ContextSourcePlan> contextPlans)
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

    private readonly record struct SourceGenerationPlan(
        AssemblyContributionSourcePlan AssemblyContribution,
        HostGenerationSourcePlan HostGeneration);

    private readonly record struct AssemblyContributionSourcePlan(
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        PipelineContributions PipelineContributions);

    private readonly record struct HostGenerationSourcePlan(
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        ImmutableArray<ContextSourcePlan> Contexts);

    private readonly record struct ContextSourcePlan(
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        bool ShouldEmitPipelines,
        bool ShouldEmitPipelineMaps,
        PipelineContributions PipelineContributions,
        PipelinePlan? PipelinePlan);
}

