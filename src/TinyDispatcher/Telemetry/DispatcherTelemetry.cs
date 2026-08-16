using System;
using System.Diagnostics;

namespace TinyDispatcher;

internal static class DispatcherTelemetry
{
    private const string OperationNameAttribute = "tiny.operation.name";
    private const string OperationIdentityAttribute = "tiny.operation.identity";
    private const string OperationTypeAttribute = "tiny.operation.type";
    private const string OperationHandlerAttribute = "tiny.operation.handler";
    private const string OperationOutcomeAttribute = "tiny.operation.outcome";

    private const string CommandOperationType = "command";
    private const string QueryOperationType = "query";
    private const string SuccessOutcome = "success";

    private static readonly ActivitySource ActivitySource = new(
        TinyDispatcherTelemetry.ActivitySourceName,
        typeof(TinyDispatcherTelemetry).Assembly.GetName().Version?.ToString());

    internal static Activity? StartCommand<TCommand>(Type handlerType)
        where TCommand : ICommand
    {
        return StartOperation(typeof(TCommand), handlerType, CommandOperationType);
    }

    internal static Activity? StartQuery<TQuery>(Type handlerType)
    {
        return StartOperation(typeof(TQuery), handlerType, QueryOperationType);
    }

    internal static void CompleteSuccessfully(Activity? activity)
    {
        activity?.SetTag(OperationOutcomeAttribute, SuccessOutcome);
    }

    private static Activity? StartOperation(
        Type operationType,
        Type handlerType,
        string operationKind)
    {
        var operationName = operationType.Name;
        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal);

        if (activity is null)
        {
            return null;
        }

        activity.SetTag(OperationNameAttribute, operationName);
        activity.SetTag(OperationIdentityAttribute, GetTypeIdentity(operationType));
        activity.SetTag(OperationTypeAttribute, operationKind);
        activity.SetTag(OperationHandlerAttribute, GetTypeIdentity(handlerType));

        return activity;
    }

    private static string GetTypeIdentity(Type type)
    {
        return type.FullName ?? type.Name;
    }
}
