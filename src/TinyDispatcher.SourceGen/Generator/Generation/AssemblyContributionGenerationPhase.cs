using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal sealed class AssemblyContributionGenerationPhase
{
    public AssemblyContributionSourcePlan Plan(
        GeneratorOptions options,
        AssemblyContributionComposition assemblyContribution)
    {
        return new AssemblyContributionSourcePlan(
            Discovery: assemblyContribution.Discovery,
            EmitOptions: BuildEmitOptions(options),
            PipelineContributions: PipelineContributions.Create(PipelineConfig.Empty));
    }

    public void Generate(
        IGeneratorContext context,
        AssemblyContributionSourcePlan assemblyContribution,
        HostGenerationSourcePlan hostGeneration)
    {
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            hostGeneration.Discovery,
            hostGeneration.EmitOptions,
            hasPipelineContributions: HasPipelinePlans(hostGeneration.Contexts));

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);

        new EmptyPipelineContributionEmitter().Emit(
            context,
            assemblyContribution.Discovery,
            assemblyContribution.PipelineContributions,
            assemblyContribution.EmitOptions,
            GetPipelineRegistrationMethodNames(hostGeneration.Contexts),
            GetPipelineContributionSources(hostGeneration.Contexts));

        var handlerRegistrationsPlan = HandlerRegistrationsPlanner.Build(
            assemblyContribution.Discovery,
            assemblyContribution.EmitOptions);

        new HandlerRegistrationsEmitter().Emit(context, handlerRegistrationsPlan);
    }

    private static ImmutableArray<string> GetPipelineRegistrationMethodNames(
        ImmutableArray<HostContextSourcePlan> contextPlans)
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
        ImmutableArray<HostContextSourcePlan> contextPlans)
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

    private static bool HasPipelinePlans(ImmutableArray<HostContextSourcePlan> contextPlans)
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
}
