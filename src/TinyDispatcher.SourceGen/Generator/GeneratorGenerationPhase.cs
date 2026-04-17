#nullable enable

using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator;

internal sealed class GeneratorGenerationPhase
{
    public void Generate(
        IGeneratorContext context,
        GeneratorAnalysis analysis,
        GeneratorValidationResult validation)
    {
        var validationContext = validation.Context;
        var emitOptions = BuildEmitOptions(analysis, validationContext);
        var extraction = analysis.Extraction;

        new ModuleInitializerEmitter().Emit(context, extraction.Discovery, emitOptions);
        new EmptyPipelineContributionEmitter().Emit(context, extraction.Discovery, emitOptions);
        new HandlerRegistrationsEmitter().Emit(context, extraction.Discovery, emitOptions);

        if (emitOptions.EmitPipelineMap)
        {
            new PipelineMapsEmitter(
                    validationContext.Globals,
                    validationContext.PerCommand,
                    validationContext.Policies)
                .Emit(context, extraction.Discovery, emitOptions);
        }

        if (!ShouldEmitPipelines(validationContext))
        {
            return;
        }

        new PipelineEmitter(
                validationContext.Globals,
                validationContext.PerCommand,
                validationContext.Policies)
            .Emit(context, extraction.Discovery, emitOptions);
    }

    private static GeneratorOptions BuildEmitOptions(
        GeneratorAnalysis analysis,
        GeneratorValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(validationContext.ExpectedContextFqn))
        {
            return analysis.EffectiveOptions;
        }

        var options = analysis.EffectiveOptions;

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

        return HasAnyPipelineContributions(validationContext);
    }

    private static bool HasAnyPipelineContributions(GeneratorValidationContext validationContext)
    {
        return validationContext.Globals.Length > 0 ||
               validationContext.PerCommand.Count > 0 ||
               validationContext.Policies.Count > 0;
    }
}
