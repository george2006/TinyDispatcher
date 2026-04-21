#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Discovery;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class PipelineConfigExtractor
{
    private readonly TinyBootstrapInvocationExtractor _invocationExtractor = new();
    private readonly PolicySpecBuilder _policyBuilder = new();
    private readonly MiddlewareOrdering _ordering = new();

    public PipelineConfig Extract(
        Compilation compilation,
        ImmutableArray<InvocationExpressionSyntax> useTinyCallsSyntax)
    {
        var globalEntries = new List<OrderedEntry>();
        var perCommandEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();

        for (var i = 0; i < useTinyCallsSyntax.Length; i++)
        {
            var contributions = _invocationExtractor.Extract(useTinyCallsSyntax[i], compilation);

            globalEntries.AddRange(contributions.Globals);
            perCommandEntries.AddRange(contributions.PerCommand);
            policyTypeSymbols.AddRange(contributions.Policies);
        }

        return new PipelineConfig(
            _ordering.OrderAndDistinctGlobals(globalEntries),
            _ordering.BuildPerCommandMap(perCommandEntries),
            _policyBuilder.Build(policyTypeSymbols));
    }
}
