#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class ContextCompositionComposer
{
    public ImmutableArray<ContextComposition> Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var hostContexts = GetHostContexts(hostBootstrap);
        var contexts = ImmutableArray.CreateBuilder<ContextComposition>(hostContexts.Length);

        for (var i = 0; i < hostContexts.Length; i++)
        {
            contexts.Add(BuildContextComposition(hostBootstrap, extraction, hostContexts[i]));
        }

        return contexts.ToImmutable();
    }

    private static ContextComposition BuildContextComposition(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        HostContextInfo hostContext)
    {
        var contextFqn = hostContext.ContextTypeFqn;
        var thisAssemblyDiscovery = FilterDiscoveryByContext(extraction.Discovery, contextFqn);
        var thisAssemblyPipeline = SelectThisAssemblyPipeline(extraction, contextFqn);
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

        return new ContextComposition(
            HostContext: hostContext,
            ContextTypeFqn: contextFqn,
            ThisAssemblyPipeline: thisAssemblyPipeline,
            HostDiscovery: hostDiscovery,
            HostPipeline: hostPipeline);
    }

    private static ImmutableArray<HostContextInfo> GetHostContexts(HostBootstrapInfo hostBootstrap)
    {
        if (!hostBootstrap.Contexts.IsDefaultOrEmpty)
        {
            return hostBootstrap.Contexts;
        }

        return ImmutableArray.Create(new HostContextInfo(
            hostBootstrap.ConfiguredContextFqn,
            ImmutableArray<UseTinyDispatcherCall>.Empty));
    }

    private static PipelineConfig SelectThisAssemblyPipeline(
        GeneratorExtraction extraction,
        string contextFqn)
    {
        var hasContext = !string.IsNullOrWhiteSpace(contextFqn);
        if (!hasContext)
        {
            return PipelineConfig.Empty;
        }

        var hasNoContextPipelines = extraction.ContextPipelines.IsDefaultOrEmpty;
        if (hasNoContextPipelines)
        {
            return PipelineConfig.Empty;
        }

        for (var i = 0; i < extraction.ContextPipelines.Length; i++)
        {
            var contextPipeline = extraction.ContextPipelines[i];
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
