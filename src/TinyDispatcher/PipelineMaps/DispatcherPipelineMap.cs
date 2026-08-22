#nullable enable

using System;
using System.Collections.Generic;

namespace TinyDispatcher.PipelineMaps;

public sealed class DispatcherPipelineMap
{
    internal DispatcherPipelineMap(
        Type pipelineType,
        Type contextType,
        IReadOnlyList<Type> commandTypes,
        IReadOnlyList<Type> globalMiddlewares,
        Type? policyType,
        IReadOnlyList<Type> policyMiddlewares,
        IReadOnlyList<Type> operationMiddlewares)
    {
        PipelineType = pipelineType;
        ContextType = contextType;
        CommandTypes = commandTypes;
        GlobalMiddlewares = globalMiddlewares;
        PolicyType = policyType;
        PolicyMiddlewares = policyMiddlewares;
        OperationMiddlewares = operationMiddlewares;
    }

    public Type PipelineType { get; }

    public Type ContextType { get; }

    public IReadOnlyList<Type> CommandTypes { get; }

    public IReadOnlyList<Type> GlobalMiddlewares { get; }

    public Type? PolicyType { get; }

    public IReadOnlyList<Type> PolicyMiddlewares { get; }

    public IReadOnlyList<Type> OperationMiddlewares { get; }
}
