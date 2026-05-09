#nullable enable

using System;
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
        GeneratorExtraction extraction,
        HostBootstrapInfo hostBootstrap)
    {
        var generationPlan = BuildGenerationPlan(options, hostBootstrap, extraction);

        EmitSharedSources(context, generationPlan);
        EmitPipelineSources(context, generationPlan);
    }

    private static void EmitSharedSources(
        IGeneratorContext context,
        GenerationPlan generationPlan)
    {
        var contextPlan = generationPlan.SharedSources;
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            contextPlan.Discovery,
            contextPlan.EmitOptions,
            hasPipelineContributions: HasPipelinePlans(generationPlan.Contexts));

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);
        new EmptyPipelineContributionEmitter().Emit(
            context,
            contextPlan.LocalDiscovery,
            contextPlan.PipelineContributions,
            contextPlan.EmitOptions,
            GetPipelineRegistrationMethodNames(generationPlan.Contexts),
            GetPipelineContributionSources(generationPlan.Contexts));

        var handlerRegistrationsPlan = HandlerRegistrationsPlanner.Build(
            contextPlan.LocalDiscovery,
            contextPlan.EmitOptions);

        new HandlerRegistrationsEmitter().Emit(context, handlerRegistrationsPlan);

        var pipelineMapsPlan = PipelineMapsPlanner.Build(
            contextPlan.Discovery,
            contextPlan.PipelineContributions,
            contextPlan.EmitOptions);

        new PipelineMapsEmitter().Emit(context, pipelineMapsPlan);
    }

    private static void EmitPipelineSources(
        IGeneratorContext context,
        GenerationPlan generationPlan)
    {
        for (var i = 0; i < generationPlan.Contexts.Length; i++)
        {
            var pipelinePlan = generationPlan.Contexts[i].PipelinePlan;
            if (pipelinePlan is null)
            {
                continue;
            }

            new PipelineEmitter().Emit(context, pipelinePlan);
        }
    }

    private static GenerationPlan BuildGenerationPlan(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var sharedSources = BuildSharedGenerationPlan(options, hostBootstrap, extraction);
        var contexts = BuildPipelineGenerationPlans(options, hostBootstrap, extraction);

        return new GenerationPlan(sharedSources, contexts);
    }

    private static PipelinePlan? BuildPipelinePlan(ContextGenerationPlan contextPlan)
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

    private static ContextGenerationPlan BuildSharedGenerationPlan(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var localPipeline = SelectLocalPipeline(extraction, hostBootstrap.ExpectedContextFqn);

        return BuildContextGenerationPlan(
            options,
            hostBootstrap,
            extraction,
            hostBootstrap.ExpectedContextFqn,
            localPipeline,
            extraction.Discovery);
    }

    private static ImmutableArray<ContextGenerationPlan> BuildPipelineGenerationPlans(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var contextFqns = GetPipelineContextFqns(hostBootstrap);
        var contextPlans = ImmutableArray.CreateBuilder<ContextGenerationPlan>(contextFqns.Length);

        for (var i = 0; i < contextFqns.Length; i++)
        {
            var contextFqn = contextFqns[i];
            var localPipeline = SelectLocalPipeline(extraction, contextFqn);
            var localDiscovery = FilterDiscoveryByContext(extraction.Discovery, contextFqn);

            var contextPlan = BuildContextGenerationPlan(
                options,
                hostBootstrap,
                extraction,
                contextFqn,
                localPipeline,
                localDiscovery);

            contextPlans.Add(contextPlan with
            {
                PipelinePlan = BuildPipelinePlan(contextPlan)
            });
        }

        return contextPlans.ToImmutable();
    }

    private static ContextGenerationPlan BuildContextGenerationPlan(
        GeneratorOptions options,
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        string contextFqn,
        PipelineConfig localPipeline,
        DiscoveryResult localDiscovery)
    {
        var emitOptions = BuildEmitOptions(options, contextFqn);
        var discovery = ReferencedAssemblyContributionComposer.MergeDiscovery(
            localDiscovery,
            extraction.ReferencedContributions,
            contextFqn);
        var pipelineConfig = ReferencedAssemblyContributionComposer.MergePipelineConfig(
            localPipeline,
            extraction.ReferencedContributions,
            contextFqn);
        var shouldEmitPipelines = ShouldEmitPipelines(hostBootstrap, contextFqn, pipelineConfig);
        var pipelineContributions = PipelineContributions.Create(pipelineConfig);

        return new ContextGenerationPlan(
            LocalDiscovery: localDiscovery,
            Discovery: discovery,
            EmitOptions: emitOptions,
            ShouldEmitPipelines: shouldEmitPipelines,
            PipelineContributions: pipelineContributions,
            PipelinePlan: null);
    }

    private static GeneratorOptions BuildEmitOptions(
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

    private static bool HasAnyPipelineContributions(PipelineConfig pipeline)
    {
        return pipeline.Globals.Length > 0 ||
               pipeline.PerCommand.Count > 0 ||
               pipeline.Policies.Count > 0;
    }

    private static ImmutableArray<string> GetPipelineRegistrationMethodNames(
        ImmutableArray<ContextGenerationPlan> contextPlans)
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
        ImmutableArray<ContextGenerationPlan> contextPlans)
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

    private static bool HasPipelinePlans(ImmutableArray<ContextGenerationPlan> contextPlans)
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

    private static ImmutableArray<string> GetPipelineContextFqns(HostBootstrapInfo hostBootstrap)
    {
        var contexts = hostBootstrap.Contexts;
        if (contexts.IsDefaultOrEmpty)
        {
            return GetFallbackContextFqns(hostBootstrap);
        }

        var contextFqns = ImmutableArray.CreateBuilder<string>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            contextFqns.Add(contexts[i].ContextTypeFqn);
        }

        return contextFqns.ToImmutable();
    }

    private static ImmutableArray<string> GetFallbackContextFqns(
        HostBootstrapInfo hostBootstrap)
    {
        if (string.IsNullOrWhiteSpace(hostBootstrap.ExpectedContextFqn))
        {
            return ImmutableArray<string>.Empty;
        }

        return ImmutableArray.Create(hostBootstrap.ExpectedContextFqn);
    }

    private static PipelineConfig SelectLocalPipeline(
        GeneratorExtraction extraction,
        string contextFqn)
    {
        if (extraction.ContextPipelines.IsDefaultOrEmpty)
        {
            return PipelineConfig.Empty;
        }

        for (var i = 0; i < extraction.ContextPipelines.Length; i++)
        {
            var contextPipeline = extraction.ContextPipelines[i];
            var isExpectedContext = string.Equals(
                contextPipeline.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isExpectedContext)
            {
                return contextPipeline.Pipeline;
            }
        }

        return PipelineConfig.Empty;
    }

    private static DiscoveryResult FilterDiscoveryByContext(
        DiscoveryResult discovery,
        string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(contextFqn))
        {
            return discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < discovery.Commands.Length; i++)
        {
            var command = discovery.Commands[i];
            var isExpectedContext = string.Equals(
                command.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isExpectedContext)
            {
                commands.Add(command);
            }
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            discovery.Queries);
    }

    private readonly record struct GenerationPlan(
        ContextGenerationPlan SharedSources,
        ImmutableArray<ContextGenerationPlan> Contexts);

    private readonly record struct ContextGenerationPlan(
        DiscoveryResult LocalDiscovery,
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        bool ShouldEmitPipelines,
        PipelineContributions PipelineContributions,
        PipelinePlan? PipelinePlan);
}

