using System;

namespace TinyDispatcher.Bootstrap;

public sealed class DispatcherOperation
{
    public DispatcherOperation(
        Type operationType,
        Type handlerType,
        DispatcherOperationKind kind,
        Type? contextType = null)
    {
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        Kind = kind;
        ContextType = contextType;
    }

    public Type OperationType { get; }

    public Type HandlerType { get; }

    public DispatcherOperationKind Kind { get; }

    public Type? ContextType { get; }
}
