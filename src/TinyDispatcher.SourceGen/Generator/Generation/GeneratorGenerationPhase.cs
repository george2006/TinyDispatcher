#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using TinyDispatcher.SourceGen.Generator.Validation;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class GeneratorGenerationPhase
{
    public void Generate(
        IGeneratorContext context,
        GeneratorOptions options,
        GeneratorExtraction extraction,
        GeneratorValidationResult validation)
    {
        var sharedPlan = BuildSharedGenerationPlan(options, validation.Context, extraction);
        var pipelinePlans = BuildPipelineGenerationPlans(options, validation.Context, extraction);

        EmitSharedSources(context, sharedPlan);
        EmitPipelineSourcesIfNeeded(context, pipelinePlans);
    }

    private static void EmitSharedSources(
        IGeneratorContext context,
        ContextGenerationPlan contextPlan)
    {
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            contextPlan.Discovery,
            contextPlan.EmitOptions,
            hasPipelineContributions: contextPlan.ShouldEmitPipelines);

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);
        new EmptyPipelineContributionEmitter().Emit(
            context,
            contextPlan.LocalDiscovery,
            contextPlan.PipelineContributions,
            contextPlan.EmitOptions);

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

    private static void EmitPipelineSourcesIfNeeded(
        IGeneratorContext context,
        ImmutableArray<ContextGenerationPlan> pipelinePlans)
    {
        for (var i = 0; i < pipelinePlans.Length; i++)
        {
            var contextPlan = pipelinePlans[i];
            if (!contextPlan.ShouldEmitPipelines)
            {
                continue;
            }

            var pipelinePlan = BuildPipelinePlan(contextPlan);

            if (!pipelinePlan.ShouldEmit)
            {
                continue;
            }

            new PipelineEmitter().Emit(context, pipelinePlan);
        }
    }

    private static PipelinePlan BuildPipelinePlan(ContextGenerationPlan contextPlan)
    {
        return PipelinePlanner.Build(
            contextPlan.PipelineContributions,
            contextPlan.Discovery,
            contextPlan.EmitOptions);
    }

    private static ContextGenerationPlan BuildSharedGenerationPlan(
        GeneratorOptions options,
        GeneratorValidationContext validationContext,
        GeneratorExtraction extraction)
    {
        return BuildContextGenerationPlan(
            options,
            validationContext,
            extraction,
            validationContext.ExpectedContextFqn,
            validationContext.LocalPipeline,
            extraction.Discovery);
    }

    private static ImmutableArray<ContextGenerationPlan> BuildPipelineGenerationPlans(
        GeneratorOptions options,
        GeneratorValidationContext validationContext,
        GeneratorExtraction extraction)
    {
        var contextFqns = GetPipelineContextFqns(validationContext);
        var contextPlans = ImmutableArray.CreateBuilder<ContextGenerationPlan>(contextFqns.Length);

        for (var i = 0; i < contextFqns.Length; i++)
        {
            var contextFqn = contextFqns[i];
            var localPipeline = SelectLocalPipeline(extraction, contextFqn);
            var localDiscovery = FilterDiscoveryByContext(extraction.Discovery, contextFqn);

            contextPlans.Add(BuildContextGenerationPlan(
                options,
                validationContext,
                extraction,
                contextFqn,
                localPipeline,
                localDiscovery));
        }

        return contextPlans.ToImmutable();
    }

    private static ContextGenerationPlan BuildContextGenerationPlan(
        GeneratorOptions options,
        GeneratorValidationContext validationContext,
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
        var shouldEmitPipelines = ShouldEmitPipelines(validationContext, contextFqn, pipelineConfig);
        var pipelineContributions = PipelineContributions.Create(pipelineConfig);

        return new ContextGenerationPlan(
            LocalDiscovery: extraction.Discovery,
            Discovery: discovery,
            EmitOptions: emitOptions,
            ShouldEmitPipelines: shouldEmitPipelines,
            PipelineContributions: pipelineContributions);
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
        GeneratorValidationContext validationContext,
        string contextFqn,
        PipelineConfig pipeline)
    {
        if (!validationContext.IsHostProject)
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

    private static ImmutableArray<string> GetPipelineContextFqns(GeneratorValidationContext validationContext)
    {
        var calls = validationContext.UseTinyDispatcherCalls;
        if (calls.IsDefaultOrEmpty)
        {
            return GetFallbackContextFqns(validationContext);
        }

        var contextFqns = ImmutableArray.CreateBuilder<string>();
        var seenContexts = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < calls.Length; i++)
        {
            var contextFqn = calls[i].ContextTypeFqn;
            var isFirstContext = seenContexts.Add(contextFqn);

            if (isFirstContext)
            {
                contextFqns.Add(contextFqn);
            }
        }

        return contextFqns.ToImmutable();
    }

    private static ImmutableArray<string> GetFallbackContextFqns(
        GeneratorValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(validationContext.ExpectedContextFqn))
        {
            return ImmutableArray<string>.Empty;
        }

        return ImmutableArray.Create(validationContext.ExpectedContextFqn);
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

    private readonly record struct ContextGenerationPlan(
        DiscoveryResult LocalDiscovery,
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        bool ShouldEmitPipelines,
        PipelineContributions PipelineContributions);
}

