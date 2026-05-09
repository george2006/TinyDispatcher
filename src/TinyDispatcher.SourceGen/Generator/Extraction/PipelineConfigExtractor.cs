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

    public ImmutableArray<ContextPipelineConfig> ExtractByContext(
        ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas)
    {
        var hasNoBootstrapLambdas = confirmedBootstrapLambdas.IsDefaultOrEmpty;
        if (hasNoBootstrapLambdas)
        {
            return ImmutableArray<ContextPipelineConfig>.Empty;
        }

        var contextOrder = new List<string>();
        var lambdasByContext = GroupLambdasByContext(confirmedBootstrapLambdas, contextOrder);
        return BuildContextPipelines(contextOrder, lambdasByContext);
    }

    private PipelineConfig BuildPipelineConfig(ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas)
    {
        var globalEntries = new List<OrderedEntry>();
        var perCommandEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();

        for (var i = 0; i < confirmedBootstrapLambdas.Length; i++)
        {
            var contributions = _invocationExtractor.Extract(confirmedBootstrapLambdas[i]);

            globalEntries.AddRange(contributions.Globals);
            perCommandEntries.AddRange(contributions.PerCommand);
            policyTypeSymbols.AddRange(contributions.Policies);
        }

        return new PipelineConfig(
            _ordering.OrderAndDistinctGlobals(globalEntries),
            _ordering.BuildPerCommandMap(perCommandEntries),
            _policyBuilder.Build(policyTypeSymbols));
    }

    private static Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> GroupLambdasByContext(
        ImmutableArray<ConfirmedBootstrapLambda> confirmedBootstrapLambdas,
        List<string> contextOrder)
    {
        var groups = new Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder>(
            StringComparer.Ordinal);

        for (var i = 0; i < confirmedBootstrapLambdas.Length; i++)
        {
            var lambda = confirmedBootstrapLambdas[i];
            var builder = GetOrAddContextBuilder(groups, contextOrder, lambda.ContextTypeFqn);
            builder.Add(lambda);
        }

        return groups;
    }

    private static ImmutableArray<ConfirmedBootstrapLambda>.Builder GetOrAddContextBuilder(
        Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> groups,
        List<string> contextOrder,
        string contextTypeFqn)
    {
        var hasExistingContext = groups.TryGetValue(contextTypeFqn, out var builder);
        if (hasExistingContext)
        {
            return builder!;
        }

        builder = ImmutableArray.CreateBuilder<ConfirmedBootstrapLambda>();
        groups.Add(contextTypeFqn, builder);
        contextOrder.Add(contextTypeFqn);

        return builder;
    }

    private ImmutableArray<ContextPipelineConfig> BuildContextPipelines(
        List<string> contextOrder,
        Dictionary<string, ImmutableArray<ConfirmedBootstrapLambda>.Builder> lambdasByContext)
    {
        var contexts = ImmutableArray.CreateBuilder<ContextPipelineConfig>(contextOrder.Count);

        for (var i = 0; i < contextOrder.Count; i++)
        {
            var contextTypeFqn = contextOrder[i];
            var pipeline = BuildPipelineConfig(lambdasByContext[contextTypeFqn].ToImmutable());

            contexts.Add(new ContextPipelineConfig(contextTypeFqn, pipeline));
        }

        return contexts.ToImmutable();
    }
}
