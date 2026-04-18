#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal sealed record PipelinePolicyContribution(
    string PolicyTypeFqn,
    MiddlewareRef[] Middlewares,
    ImmutableArray<string> Commands);
