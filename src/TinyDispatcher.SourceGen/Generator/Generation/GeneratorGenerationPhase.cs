#nullable enable

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
        var contextPlan = BuildContextGenerationPlan(options, validation.Context, extraction);

        EmitSharedSources(context, contextPlan);
        EmitPipelineSourceIfNeeded(context, contextPlan);
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

    private static void EmitPipelineSourceIfNeeded(
        IGeneratorContext context,
        ContextGenerationPlan contextPlan)
    {
        if (!contextPlan.ShouldEmitPipelines)
        {
            return;
        }

        var pipelinePlan = BuildPipelinePlan(contextPlan);

        if (!pipelinePlan.ShouldEmit)
        {
            return;
        }

        new PipelineEmitter().Emit(context, pipelinePlan);
    }

    private static PipelinePlan BuildPipelinePlan(ContextGenerationPlan contextPlan)
    {
        return PipelinePlanner.Build(
            contextPlan.PipelineContributions,
            contextPlan.Discovery,
            contextPlan.EmitOptions);
    }

    private static ContextGenerationPlan BuildContextGenerationPlan(
        GeneratorOptions options,
        GeneratorValidationContext validationContext,
        GeneratorExtraction extraction)
    {
        var emitOptions = BuildEmitOptions(options, validationContext);
        var discovery = ReferencedAssemblyContributionComposer.MergeDiscovery(
            extraction.Discovery,
            extraction.ReferencedContributions,
            validationContext.ExpectedContextFqn);
        var pipelineConfig = ReferencedAssemblyContributionComposer.MergePipelineConfig(
            validationContext.Pipeline,
            extraction.ReferencedContributions,
            validationContext.ExpectedContextFqn);
        var shouldEmitPipelines = ShouldEmitPipelines(validationContext, pipelineConfig);
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
        GeneratorValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(validationContext.ExpectedContextFqn))
        {
            return options;
        }

        return new GeneratorOptions(
            GeneratedNamespace: options.GeneratedNamespace,
            EmitDiExtensions: options.EmitDiExtensions,
            EmitHandlerRegistrations: options.EmitHandlerRegistrations,
            IncludeNamespacePrefix: options.IncludeNamespacePrefix,
            CommandContextType: validationContext.ExpectedContextFqn,
            EmitPipelineMap: options.EmitPipelineMap,
            PipelineMapFormat: options.PipelineMapFormat);
    }

    private static bool ShouldEmitPipelines(GeneratorValidationContext validationContext, PipelineConfig pipeline)
    {
        if (!validationContext.IsHostProject)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(validationContext.ExpectedContextFqn))
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

    private readonly record struct ContextGenerationPlan(
        DiscoveryResult LocalDiscovery,
        DiscoveryResult Discovery,
        GeneratorOptions EmitOptions,
        bool ShouldEmitPipelines,
        PipelineContributions PipelineContributions);
}

