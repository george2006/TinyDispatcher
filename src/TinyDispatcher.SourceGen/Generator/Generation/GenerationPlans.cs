using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation;

internal readonly record struct AssemblyContributionSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    ModuleInitializerContributionSourcePlan ModuleInitializer,
    AssemblyPipelineContributionSourcePlan PipelineContribution);

internal readonly record struct ModuleInitializerContributionSourcePlan(
    DiscoveryResult Discovery,
    bool HasPipelineContributions);

internal readonly record struct AssemblyPipelineContributionSourcePlan(
    PipelineContributions Contributions,
    ImmutableArray<string> RegistrationMethodNames,
    ImmutableArray<EmptyPipelineContributionEmitter.PipelineContributionSource> ContributionSources);

internal readonly record struct HostGenerationSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    ImmutableArray<HostContextSourcePlan> Contexts);

internal readonly record struct HostContextSourcePlan(
    DiscoveryResult Discovery,
    GeneratorOptions EmitOptions,
    bool ShouldEmitPipelines,
    bool ShouldEmitPipelineMaps,
    PipelineContributions PipelineContributions,
    PipelinePlan? PipelinePlan);
