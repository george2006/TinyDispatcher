#nullable enable

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
        var generationPlan = BuildGenerationPlan(options, validation.Context);

        EmitSharedSources(context, extraction, generationPlan);
        EmitPipelineSourceIfNeeded(context, extraction, generationPlan);
    }

    private static void EmitSharedSources(
        IGeneratorContext context,
        GeneratorExtraction extraction,
        GenerationPlan generationPlan)
    {
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            extraction.Discovery,
            generationPlan.EmitOptions,
            hasPipelineContributions: generationPlan.ShouldEmitPipelines);

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);
        new EmptyPipelineContributionEmitter().Emit(context, generationPlan.EmitOptions);

        var handlerRegistrationsPlan = HandlerRegistrationsPlanner.Build(
            extraction.Discovery,
            generationPlan.EmitOptions);

        new HandlerRegistrationsEmitter().Emit(context, handlerRegistrationsPlan);

        var pipelineMapsPlan = PipelineMapsPlanner.Build(
            extraction.Discovery,
            generationPlan.PipelineContributions,
            generationPlan.EmitOptions);

        new PipelineMapsEmitter().Emit(context, pipelineMapsPlan);
    }

    private static void EmitPipelineSourceIfNeeded(
        IGeneratorContext context,
        GeneratorExtraction extraction,
        GenerationPlan generationPlan)
    {
        if (!generationPlan.ShouldEmitPipelines)
        {
            return;
        }

        var pipelinePlan = PipelinePlanner.Build(
            generationPlan.PipelineContributions,
            extraction.Discovery,
            generationPlan.EmitOptions);

        if (!pipelinePlan.ShouldEmit)
        {
            return;
        }

        new PipelineEmitter().Emit(context, pipelinePlan);
    }

    private static GenerationPlan BuildGenerationPlan(
        GeneratorOptions options,
        GeneratorValidationContext validationContext)
    {
        var emitOptions = BuildEmitOptions(options, validationContext);
        var shouldEmitPipelines = ShouldEmitPipelines(validationContext);
        var pipelineContributions = PipelineContributions.Create(validationContext.Pipeline);

        return new GenerationPlan(
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

    private static bool ShouldEmitPipelines(GeneratorValidationContext validationContext)
    {
        if (!validationContext.IsHostProject)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(validationContext.ExpectedContextFqn))
        {
            return false;
        }

        return HasAnyPipelineContributions(validationContext.Pipeline);
    }

    private static bool HasAnyPipelineContributions(PipelineConfig pipeline)
    {
        return pipeline.Globals.Length > 0 ||
               pipeline.PerCommand.Count > 0 ||
               pipeline.Policies.Count > 0;
    }

    private readonly record struct GenerationPlan(
        GeneratorOptions EmitOptions,
        bool ShouldEmitPipelines,
        PipelineContributions PipelineContributions);
}

