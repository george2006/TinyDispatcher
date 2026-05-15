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
        AssemblyContributionModel assemblyContribution)
    {
        var emitOptions = GenerationEmitOptions.ForAssemblyContribution(options);
        var pipelineContributions = PipelineContributions.Create(PipelineConfig.Empty);

        var moduleInitializer = new ModuleInitializerSourcePlan(
            Discovery: BuildModuleInitializerDiscovery(assemblyContribution),
            HasPipelineContributions: HasPipelineContributions(
                assemblyContribution.IsHostProject,
                options,
                assemblyContribution.Lanes));

        var pipelineContribution = new AssemblyPipelineContributionSourcePlan(
            Contributions: pipelineContributions,
            RegistrationMethodNames: GetPipelineRegistrationMethodNames(
                assemblyContribution.IsHostProject,
                options,
                assemblyContribution.Lanes),
            ContributionSources: GetPipelineContributionSources(
                options,
                assemblyContribution.Lanes));

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

        new ThisAssemblyContributionEmitter().Emit(
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
        ImmutableArray<HostLane> lanes)
    {
        if (lanes.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var methodNames = ImmutableArray.CreateBuilder<string>(lanes.Length);

        for (var i = 0; i < lanes.Length; i++)
        {
            var pipelinePlan = BuildPipelinePlan(
                isHostProject,
                options,
                lanes[i]);
            if (pipelinePlan is null)
            {
                continue;
            }

            methodNames.Add(PipelineNameFactory.PipelineRegistrationMethodName(
                pipelinePlan.ContextFqn));
        }

        return methodNames.ToImmutable();
    }

    private static ImmutableArray<PipelineContributionSource> GetPipelineContributionSources(
        GeneratorOptions options,
        ImmutableArray<HostLane> lanes)
    {
        if (lanes.IsDefaultOrEmpty)
        {
            return ImmutableArray<PipelineContributionSource>.Empty;
        }

        var sources = ImmutableArray.CreateBuilder<PipelineContributionSource>(lanes.Length);

        for (var i = 0; i < lanes.Length; i++)
        {
            var lane = lanes[i];
            sources.Add(new PipelineContributionSource(
                GenerationEmitOptions.ForContextLane(options, lane.ContextTypeFqn),
                PipelineContributions.Create(lane.Pipeline)));
        }

        return sources.ToImmutable();
    }

    private static bool HasPipelineContributions(
        bool isHostProject,
        GeneratorOptions options,
        ImmutableArray<HostLane> lanes)
    {
        for (var i = 0; i < lanes.Length; i++)
        {
            if (BuildPipelinePlan(isHostProject, options, lanes[i]) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static DiscoveryResult BuildModuleInitializerDiscovery(
        AssemblyContributionModel assemblyContribution)
    {
        if (assemblyContribution.Lanes.IsDefaultOrEmpty)
        {
            return assemblyContribution.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < assemblyContribution.Lanes.Length; i++)
        {
            commands.AddRange(assemblyContribution.Lanes[i].Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            assemblyContribution.Discovery.Queries);
    }

    private static PipelinePlan? BuildPipelinePlan(
        bool isHostProject,
        GeneratorOptions options,
        HostLane lane)
    {
        if (!GenerationEmissionRules.ShouldEmitPipelines(
                isHostProject,
                lane.ContextTypeFqn,
                lane.Pipeline))
        {
            return null;
        }

        var pipelinePlan = PipelinePlanner.Build(
            PipelineContributions.Create(lane.Pipeline),
            lane.Discovery,
            GenerationEmitOptions.ForContextLane(options, lane.ContextTypeFqn));

        if (!pipelinePlan.ShouldEmit)
        {
            return null;
        }

        return pipelinePlan;
    }

}
