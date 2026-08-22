using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal sealed record PipelineMapAttributeDescriptor(
    string PipelineTypeExpression,
    string ContextFullName,
    ImmutableArray<string> CommandFullNames,
    ImmutableArray<MiddlewareRef> Middlewares,
    int GlobalMiddlewareCount,
    string? PolicyFullName,
    int PolicyMiddlewareCount);
