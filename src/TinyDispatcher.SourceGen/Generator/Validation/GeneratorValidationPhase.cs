#nullable enable

using System;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Generation;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public DiagnosticBag Validate(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog,
        ValidationRoslynDependencies roslynDependencies)
    {
        var validationContexts = BuildValidationContexts(
            hostBootstrap,
            extraction,
            diagnosticsCatalog);
        var diagnostics = new DiagnosticBag();

        for (var i = 0; i < validationContexts.Length; i++)
        {
            var contextDiagnostics = GeneratorValidator.Validate(
                validationContexts[i],
                roslynDependencies.CommandMiddlewareInterface,
                roslynDependencies.MiddlewareTypeResolver);

            diagnostics.AddRange(contextDiagnostics.ToImmutable());
        }

        return diagnostics;
    }

    private static ImmutableArray<GeneratorValidationContext> BuildValidationContexts(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var hostContexts = GetHostContexts(hostBootstrap);
        var validationContexts = ImmutableArray.CreateBuilder<GeneratorValidationContext>(hostContexts.Length);

        for (var i = 0; i < hostContexts.Length; i++)
        {
            var hostContext = hostContexts[i];
            validationContexts.Add(BuildValidationContext(
                hostBootstrap,
                extraction,
                diagnosticsCatalog,
                hostContext.ContextTypeFqn,
                hostContext.UseTinyDispatcherCalls));
        }

        return validationContexts.ToImmutable();
    }

    private static GeneratorValidationContext BuildValidationContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog,
        string contextFqn,
        ImmutableArray<UseTinyDispatcherCall> useTinyDispatcherCalls)
    {
        var discovery = BuildDiscovery(hostBootstrap, extraction, contextFqn);
        var localPipeline = SelectLocalPipeline(extraction, contextFqn);
        var pipeline = BuildPipeline(hostBootstrap, extraction, contextFqn, localPipeline);

        return new GeneratorValidationContext.Builder(
                discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(useTinyDispatcherCalls)
            .WithExpectedContext(contextFqn)
            .WithReferencedContributions(extraction.ReferencedContributions)
            .WithLocalPipelineConfig(localPipeline)
            .WithPipelineConfig(pipeline)
            .Build();
    }

    private static ImmutableArray<HostContextInfo> GetHostContexts(HostBootstrapInfo hostBootstrap)
    {
        if (!hostBootstrap.Contexts.IsDefaultOrEmpty)
        {
            return hostBootstrap.Contexts;
        }

        return ImmutableArray.Create(new HostContextInfo(
            hostBootstrap.ExpectedContextFqn,
            hostBootstrap.UseTinyDispatcherCalls));
    }

    private static DiscoveryResult BuildDiscovery(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        string contextFqn)
    {
        var localDiscovery = FilterDiscoveryByContext(extraction.Discovery, contextFqn);

        if (!ShouldMergeReferencedContributions(hostBootstrap, contextFqn))
            return localDiscovery;

        return ReferencedAssemblyContributionComposer.MergeDiscovery(
            localDiscovery,
            extraction.ReferencedContributions,
            contextFqn);
    }

    private static PipelineConfig BuildPipeline(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        string contextFqn,
        PipelineConfig localPipeline)
    {
        if (!ShouldMergeReferencedContributions(hostBootstrap, contextFqn))
            return localPipeline;

        return ReferencedAssemblyContributionComposer.MergePipelineConfig(
            localPipeline,
            extraction.ReferencedContributions,
            contextFqn);
    }

    private static PipelineConfig SelectLocalPipeline(
        GeneratorExtraction extraction,
        string contextFqn)
    {
        var hasExpectedContext = !string.IsNullOrWhiteSpace(contextFqn);
        if (!hasExpectedContext)
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
            var isExpectedContext = string.Equals(
                contextPipeline.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isExpectedContext)
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
            var isExpectedContext = string.Equals(
                command.ContextTypeFqn,
                contextFqn,
                StringComparison.Ordinal);

            if (isExpectedContext)
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
