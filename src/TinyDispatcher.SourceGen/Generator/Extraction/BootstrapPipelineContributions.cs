#nullable enable

using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed record BootstrapPipelineContributions(
    ImmutableArray<OrderedEntry> Globals,
    ImmutableArray<OrderedPerCommandEntry> PerCommand,
    ImmutableArray<INamedTypeSymbol> Policies);
