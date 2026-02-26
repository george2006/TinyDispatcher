using System.Collections.Generic;

namespace TinyDispatcher.SourceGen.Emitters.PipelineMaps;

internal sealed record PipelineDescriptor(
    string CommandFullName,
    string ContextFullName,
    string HandlerFullName,
    IReadOnlyList<MiddlewareDescriptor> Middlewares,
    IReadOnlyList<string> PoliciesApplied);

internal sealed record MiddlewareDescriptor(
    string MiddlewareFullName,
    string Source);