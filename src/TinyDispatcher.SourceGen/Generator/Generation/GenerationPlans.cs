using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal readonly record struct AssemblyContributionSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    ModuleInitializerSourcePlan ModuleInitializer,
    AssemblyPipelineContributionSourcePlan PipelineContribution);

internal readonly record struct ModuleInitializerSourcePlan(
    DiscoveryResult Discovery,
    bool HasPipelineContributions);

internal readonly record struct AssemblyPipelineContributionSourcePlan(
    PipelineContributions Contributions,
    ImmutableArray<string> RegistrationMethodNames,
    ImmutableArray<PipelineContributionSource> ContributionSources);

internal readonly record struct PipelineContributionSource(
    GeneratorOptions Options,
    PipelineContributions Contributions);

internal readonly record struct HostGenerationSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    ImmutableArray<HostLaneSourcePlan> Lanes);

internal readonly record struct HostLaneSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    bool ShouldEmitPipelines,
    bool ShouldEmitPipelineMaps,
    PipelineContributions PipelineContributions,
    PipelinePlan? PipelinePlan);
