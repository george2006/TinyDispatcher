#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class HostGenerationComposer
{
    public HostGenerationComposition Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var contexts = BuildHostContexts(hostBootstrap, extraction);
        var discovery = BuildHostDiscovery(extraction.ThisAssembly, contexts);

        return new HostGenerationComposition(
            discovery,
            extraction.ReferencedContributions,
            contexts);
    }

    private static ImmutableArray<HostContextProjection> BuildHostContexts(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var hostContexts = GetHostContexts(hostBootstrap);
        var contexts = ImmutableArray.CreateBuilder<HostContextProjection>(hostContexts.Length);

        for (var i = 0; i < hostContexts.Length; i++)
        {
            contexts.Add(BuildHostContext(hostBootstrap, extraction, hostContexts[i]));
        }

        return contexts.ToImmutable();
    }

    private static HostContextProjection BuildHostContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        HostContextInfo hostContext)
    {
        var contextFqn = hostContext.ContextTypeFqn;
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

        var generationInput = new ContextGenerationInput(
            ContextTypeFqn: contextFqn,
            Discovery: hostDiscovery,
            Pipeline: hostPipeline);

        return new HostContextProjection(
            HostContext: hostContext,
            ThisAssemblyPipeline: thisAssemblyPipeline,
            GenerationInput: generationInput);
    }

    private static DiscoveryResult BuildHostDiscovery(
        ThisAssemblyExtraction thisAssembly,
        ImmutableArray<HostContextProjection> contexts)
    {
        if (contexts.IsDefaultOrEmpty)
        {
            return thisAssembly.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < contexts.Length; i++)
        {
            commands.AddRange(contexts[i].GenerationInput.Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            thisAssembly.Discovery.Queries);
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
