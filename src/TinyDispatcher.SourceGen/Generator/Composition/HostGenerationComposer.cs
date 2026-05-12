#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class HostGenerationComposer
{
    public HostModel Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var lanes = BuildHostLanes(hostBootstrap, extraction);
        var discovery = BuildHostDiscovery(extraction.ThisAssembly, lanes);

        return new HostModel(
            discovery,
            lanes);
    }

    private static ImmutableArray<HostLane> BuildHostLanes(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var laneDeclarations = GetHostLaneDeclarations(hostBootstrap);
        var lanes = ImmutableArray.CreateBuilder<HostLane>(laneDeclarations.Length);

        for (var i = 0; i < laneDeclarations.Length; i++)
        {
            lanes.Add(BuildHostLane(hostBootstrap, extraction, laneDeclarations[i]));
        }

        return lanes.ToImmutable();
    }

    private static HostLane BuildHostLane(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        HostLaneDeclaration declaration)
    {
        var contextFqn = declaration.ContextTypeFqn;
        var thisAssemblyDiscovery = FilterDiscoveryByContext(
            extraction.ThisAssembly.Discovery,
            contextFqn);
        var thisAssemblyPipeline = SelectThisAssemblyPipeline(
            extraction.ThisAssembly,
            contextFqn);
        var hostDiscovery = thisAssemblyDiscovery;
        var hostPipeline = thisAssemblyPipeline;
        var shouldMergeReferencedContributions = ShouldMergeReferencedContributions(
            hostBootstrap,
            contextFqn);

        if (shouldMergeReferencedContributions)
        {
            hostDiscovery = ReferencedAssemblyContributionComposer.MergeDiscovery(
                thisAssemblyDiscovery,
                extraction.ReferencedContributions,
                contextFqn);
            hostPipeline = ReferencedAssemblyContributionComposer.MergePipelineConfig(
                thisAssemblyPipeline,
                extraction.ReferencedContributions,
                contextFqn);
        }

        return new HostLane(
            Declaration: declaration,
            ThisAssemblyPipeline: thisAssemblyPipeline,
            Discovery: hostDiscovery,
            Pipeline: hostPipeline);
    }

    private static DiscoveryResult BuildHostDiscovery(
        ThisAssemblyExtraction thisAssembly,
        ImmutableArray<HostLane> lanes)
    {
        if (lanes.IsDefaultOrEmpty)
        {
            return thisAssembly.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < lanes.Length; i++)
        {
            commands.AddRange(lanes[i].Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            thisAssembly.Discovery.Queries);
    }

    private static ImmutableArray<HostLaneDeclaration> GetHostLaneDeclarations(HostBootstrapInfo hostBootstrap)
    {
        if (!hostBootstrap.LaneDeclarations.IsDefaultOrEmpty)
        {
            return hostBootstrap.LaneDeclarations;
        }

        return ImmutableArray.Create(new HostLaneDeclaration(
            hostBootstrap.ConfiguredContextFqn,
            ImmutableArray<UseTinyDispatcherCall>.Empty));
    }

    private static PipelineConfig SelectThisAssemblyPipeline(
        ThisAssemblyExtraction thisAssembly,
        string contextFqn)
    {
        var hasContext = !string.IsNullOrWhiteSpace(contextFqn);
        if (!hasContext)
        {
            return PipelineConfig.Empty;
        }

        var contextPipelines = thisAssembly.ContextPipelines;
        var hasNoContextPipelines = contextPipelines.IsDefaultOrEmpty;
        if (hasNoContextPipelines)
        {
            return PipelineConfig.Empty;
        }

        for (var i = 0; i < contextPipelines.Length; i++)
        {
            var contextPipeline = contextPipelines[i];
            var isContextPipeline = string.Equals(
                contextPipeline.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isContextPipeline)
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
            var isContextCommand = string.Equals(
                command.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isContextCommand)
            {
                commands.Add(command);
            }
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            discovery.Queries);
    }

    private static bool ShouldMergeReferencedContributions(
        HostBootstrapInfo hostBootstrap,
        string contextFqn)
    {
        return hostBootstrap.IsHostProject &&
               !string.IsNullOrWhiteSpace(contextFqn);
    }
}
