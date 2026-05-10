#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Composition;

internal sealed class GeneratorCompositionPhase
{
    public GeneratorContextComposition Compose(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var composedContexts = BuildContexts(hostBootstrap, extraction);
        var generationContexts = SelectGenerationInputs(composedContexts);
        var validationContexts = SelectValidationInputs(composedContexts);
        var discovery = BuildSharedDiscovery(extraction, generationContexts);

        return new GeneratorContextComposition(
            LocalDiscovery: extraction.Discovery,
            Discovery: discovery,
            ReferencedContributions: extraction.ReferencedContributions,
            GenerationContexts: generationContexts,
            ValidationContexts: validationContexts);
    }

    private static ImmutableArray<ComposedContextInput> BuildContexts(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction)
    {
        var hostContexts = GetHostContexts(hostBootstrap);
        var contexts = ImmutableArray.CreateBuilder<ComposedContextInput>(hostContexts.Length);

        for (var i = 0; i < hostContexts.Length; i++)
        {
            contexts.Add(BuildContext(hostBootstrap, extraction, hostContexts[i]));
        }

        return contexts.ToImmutable();
    }

    private static ComposedContextInput BuildContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        HostContextInfo hostContext)
    {
        var contextFqn = hostContext.ContextTypeFqn;
        var localDiscovery = FilterDiscoveryByContext(extraction.Discovery, contextFqn);
        var localPipeline = SelectLocalPipeline(extraction, contextFqn);
        var discovery = localDiscovery;
        var pipeline = localPipeline;
        var shouldMergeReferencedContributions = ShouldMergeReferencedContributions(
            hostBootstrap,
            contextFqn);

        if (shouldMergeReferencedContributions)
        {
            discovery = ReferencedAssemblyContributionComposer.MergeDiscovery(
                localDiscovery,
                extraction.ReferencedContributions,
                contextFqn);
            pipeline = ReferencedAssemblyContributionComposer.MergePipelineConfig(
                localPipeline,
                extraction.ReferencedContributions,
                contextFqn);
        }

        var generationInput = new ContextGenerationInput(
            ContextTypeFqn: contextFqn,
            LocalDiscovery: localDiscovery,
            Discovery: discovery,
            LocalPipeline: localPipeline,
            Pipeline: pipeline);

        var validationInput = new ContextValidationInput(
            BootstrapCalls: hostContext.UseTinyDispatcherCalls,
            GenerationInput: generationInput);

        return new ComposedContextInput(generationInput, validationInput);
    }

    private static ImmutableArray<ContextGenerationInput> SelectGenerationInputs(
        ImmutableArray<ComposedContextInput> contexts)
    {
        var generationInputs = ImmutableArray.CreateBuilder<ContextGenerationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            generationInputs.Add(contexts[i].GenerationInput);
        }

        return generationInputs.ToImmutable();
    }

    private static ImmutableArray<ContextValidationInput> SelectValidationInputs(
        ImmutableArray<ComposedContextInput> contexts)
    {
        var validationInputs = ImmutableArray.CreateBuilder<ContextValidationInput>(contexts.Length);

        for (var i = 0; i < contexts.Length; i++)
        {
            validationInputs.Add(contexts[i].ValidationInput);
        }

        return validationInputs.ToImmutable();
    }

    private static DiscoveryResult BuildSharedDiscovery(
        GeneratorExtraction extraction,
        ImmutableArray<ContextGenerationInput> contexts)
    {
        if (contexts.IsDefaultOrEmpty)
        {
            return extraction.Discovery;
        }

        var commands = ImmutableArray.CreateBuilder<HandlerContract>();

        for (var i = 0; i < contexts.Length; i++)
        {
            commands.AddRange(contexts[i].Discovery.Commands);
        }

        return new DiscoveryResult(
            commands.ToImmutable(),
            extraction.Discovery.Queries);
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

    private static PipelineConfig SelectLocalPipeline(
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

    private readonly record struct ComposedContextInput(
        ContextGenerationInput GenerationInput,
        ContextValidationInput ValidationInput);
}
