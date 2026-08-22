using System;

namespace TinyDispatcher.Bootstrap;

public sealed class DispatcherOperationStructure
{
    public DispatcherOperationStructure(
        Type operationType,
        Type handlerType,
        DispatcherOperationKind kind,
        Type? contextType = null)
    {
        OperationType = operationType ?? throw new ArgumentNullException(nameof(operationType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        if (kind != DispatcherOperationKind.Command && kind != DispatcherOperationKind.Query)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown dispatcher operation kind.");
        }

        Kind = kind;
        ContextType = contextType;
    }

    public Type OperationType { get; }

    public Type HandlerType { get; }

    public DispatcherOperationKind Kind { get; }

    public Type? ContextType { get; }
}
