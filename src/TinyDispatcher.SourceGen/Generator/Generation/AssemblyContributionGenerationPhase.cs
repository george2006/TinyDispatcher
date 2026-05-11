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
        var emitOptions = BuildEmitOptions(options);
        var pipelineContributions = PipelineContributions.Create(PipelineConfig.Empty);

        var moduleInitializer = new ModuleInitializerContributionSourcePlan(
            Discovery: BuildModuleInitializerDiscovery(assemblyContribution),
            HasPipelineContributions: HasPipelineContributions(
                assemblyContribution.IsHostProject,
                options,
                assemblyContribution.Contexts));

        var pipelineContribution = new AssemblyPipelineContributionSourcePlan(
            Contributions: pipelineContributions,
            RegistrationMethodNames: GetPipelineRegistrationMethodNames(
                assemblyContribution.IsHostProject,
                options,
                assemblyContribution.Contexts),
            ContributionSources: GetPipelineContributionSources(
                options,
                assemblyContribution.Contexts));

        return new AssemblyContributionSourcePlan(
            Discovery: assemblyContribution.Discovery,
            EmitOptions: emitOptions,
            ModuleInitializer: moduleInitializer,
            PipelineContribution: pipelineContribution);
    }

    public void Generate(
        IGeneratorContext context,
        AssemblyContributionSourcePlan assemblyContribution)
    {
        var moduleInitializerPlan = ModuleInitializerPlanner.Build(
            assemblyContribution.ModuleInitializer.Discovery,
            assemblyContribution.EmitOptions,
            assemblyContribution.ModuleInitializer.HasPipelineContributions);

        new ModuleInitializerEmitter().Emit(context, moduleInitializerPlan);

        new EmptyPipelineContributionEmitter().Emit(
            context,
            assemblyContribution.Discovery,
            assemblyContribution.PipelineContribution.Contributions,
            assemblyContribution.EmitOptions,
            assemblyContribution.PipelineContribution.RegistrationMethodNames,
            assemblyContribution.PipelineContribution.ContributionSources);

        var handlerRegistrationsPlan = HandlerRegistrationsPlanner.Build(
            assemblyContribution.Discovery,
            assemblyContribution.EmitOptions);

        new HandlerRegistrationsEmitter().Emit(context, handlerRegistrationsPlan);
    }

    private static ImmutableArray<string> GetPipelineRegistrationMethodNames(
        bool isHostProject,
        GeneratorOptions options,
        ImmutableArray<HostContextProjection> contexts)
    {
        if (contexts.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var methodNames = ImmutableArray.CreateBuilder<string>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            var pipelinePlan = BuildPipelinePlan(
                isHostProject,
                options,
                contexts[i]);
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
        GeneratorOptions options,
        ImmutableArray<HostContextProjection> contexts)
    {
        if (contexts.IsDefaultOrEmpty)
        {
            return ImmutableArray<EmptyPipelineContributionEmitter.PipelineContributionSource>.Empty;
        }

        var sources = ImmutableArray.CreateBuilder<EmptyPipelineContributionEmitter.PipelineContributionSource>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            var contextInput = contexts[i].GenerationInput;
            sources.Add(new EmptyPipelineContributionEmitter.PipelineContributionSource(
                BuildContextEmitOptions(options, contextInput.ContextTypeFqn),
                PipelineContributions.Create(contextInput.Pipeline)));
        }

        return sources.ToImmutable();
    }

    private static bool HasPipelineContributions(
        bool isHostProject,
        GeneratorOptions options,
        ImmutableArray<HostContextProjection> contexts)
    {
        for (var i = 0; i < contexts.Length; i++)
        {
            if (BuildPipelinePlan(isHostProject, options, contexts[i]) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static DiscoveryResult BuildModuleInitializerDiscovery(
        AssemblyContributionComposition assemblyContribution)
    {
        if (assemblyContribution.Contexts.IsDefaultOrEmpty)
        {
            return assemblyContribution.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < assemblyContribution.Contexts.Length; i++)
        {
            commands.AddRange(assemblyContribution.Contexts[i].GenerationInput.Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            assemblyContribution.Discovery.Queries);
    }

    private static PipelinePlan? BuildPipelinePlan(
        bool isHostProject,
        GeneratorOptions options,
        HostContextProjection context)
    {
        var contextInput = context.GenerationInput;
        if (!ShouldEmitPipelines(
                isHostProject,
                contextInput.ContextTypeFqn,
                contextInput.Pipeline))
        {
            return null;
        }

        var pipelinePlan = PipelinePlanner.Build(
            PipelineContributions.Create(contextInput.Pipeline),
            contextInput.Discovery,
            BuildContextEmitOptions(options, contextInput.ContextTypeFqn));

        if (!pipelinePlan.ShouldEmit)
        {
            return null;
        }

        return pipelinePlan;
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
        bool isHostProject,
        string contextFqn,
        PipelineConfig pipeline)
    {
        if (!isHostProject)
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
