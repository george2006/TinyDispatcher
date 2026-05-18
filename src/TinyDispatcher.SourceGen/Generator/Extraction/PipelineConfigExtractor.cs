#nullable enable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class PipelineConfigExtractor
{
    private readonly TinyBootstrapInvocationExtractor _invocationExtractor = new();
    private readonly PolicySpecBuilder _policyBuilder = new();
    private readonly MiddlewareOrdering _ordering = new();

    public ImmutableArray<ContextPipeline> ExtractByContext(
        ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas)
    {
        var hasNoBootstrapLambdas = confirmedBootstrapLambdas.IsDefaultOrEmpty;
        if (hasNoBootstrapLambdas)
        {
            return ImmutableArray<ContextPipeline>.Empty;
        }

        var contextOrder = new List<string>();
        var bootstrapLambdasByContext = GroupBootstrapLambdasByContext(
            confirmedBootstrapLambdas,
            contextOrder);

        return BuildContextPipelines(contextOrder, bootstrapLambdasByContext);
    }

    private PipelineConfig BuildPipelineConfig(ImmutableArray<ConfirmedBootstrapLambda> contextBootstrapLambdas)
    {
        var globalEntries = new List<OrderedEntry>();
        var perCommandEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();

        for (var i = 0; i < contextBootstrapLambdas.Length; i++)
        {
            var contributions = _invocationExtractor.Extract(contextBootstrapLambdas[i]);

            globalEntries.AddRange(contributions.Globals);
            perCommandEntries.AddRange(contributions.PerCommand);
            policyTypeSymbols.AddRange(contributions.Policies);
        }

        return new PipelineConfig(
            _ordering.OrderAndDistinctGlobals(globalEntries),
            _ordering.BuildPerCommandMap(perCommandEntries),
            _policyBuilder.Build(policyTypeSymbols));
    }

    private static Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> GroupBootstrapLambdasByContext(
        ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas,
        List<string> contextOrder)
    {
        var bootstrapLambdasByContext = new Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder>(
            StringComparer.Ordinal);

        for (var i = 0; i < confirmedBootstrapLambdas.Length; i++)
        {
            var bootstrapLambda = confirmedBootstrapLambdas[i];
            var contextGroup = GetOrAddContextGroup(
                bootstrapLambdasByContext,
                contextOrder,
                bootstrapLambda.ContextTypeFqn);

            contextGroup.Add(bootstrapLambda);
        }

        return bootstrapLambdasByContext;
    }

    private static ImmutableArray<ConfirmedBootstrapLambda>.Builder GetOrAddContextGroup(
        Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> bootstrapLambdasByContext,
        List<string> contextOrder,
        string contextTypeFqn)
    {
        var hasExistingContext = bootstrapLambdasByContext.TryGetValue(contextTypeFqn, out var contextGroup);
        if (hasExistingContext)
        {
            return contextGroup!;
        }

        contextGroup = ImmutableArray.CreateBuilder<ConfirmedBootstrapLambda>();
        bootstrapLambdasByContext.Add(contextTypeFqn, contextGroup);
        contextOrder.Add(contextTypeFqn);

        return contextGroup;
    }

    private ImmutableArray<ContextPipeline> BuildContextPipelines(
        List<string> contextOrder,
        Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> bootstrapLambdasByContext)
    {
        var contextPipelines = ImmutableArray.CreateBuilder<ContextPipeline>(contextOrder.Count);

        for (var i = 0; i < contextOrder.Count; i++)
        {
            var contextTypeFqn = contextOrder[i];
            var contextPipeline = BuildPipelineConfig(bootstrapLambdasByContext[contextTypeFqn].ToImmutable());

            contextPipelines.Add(new ContextPipeline(contextTypeFqn, contextPipeline));
        }

        return contextPipelines.ToImmutable();
    }
}
