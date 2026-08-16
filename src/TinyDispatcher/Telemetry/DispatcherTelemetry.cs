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
    private const string ErrorTypeAttribute = "error.type";

    private const string CommandOperationType = "command";
    private const string QueryOperationType = "query";
    private const string SuccessOutcome = "success";
    private const string FailureOutcome = "failure";
    private const string CanceledOutcome = "canceled";

    private static readonly ActivitySource ActivitySource = new(
        TinyDispatcherTelemetry.ActivitySourceName,
        typeof(TinyDispatcherTelemetry).Assembly.GetName().Version?.ToString());

    internal static Activity? StartCommand<TCommand>()
        where TCommand : ICommand
    {
        return StartOperation(typeof(TCommand), CommandOperationType);
    }

    internal static Activity? StartQuery<TQuery>()
    {
        return StartOperation(typeof(TQuery), QueryOperationType);
    }

    internal static void SetHandler(Activity? activity, Type handlerType)
    {
        activity?.SetTag(OperationHandlerAttribute, GetTypeIdentity(handlerType));
    }

    internal static void CompleteSuccessfully(Activity? activity)
    {
        activity?.SetTag(OperationOutcomeAttribute, SuccessOutcome);
    }

    internal static void CompleteWithFailure(Activity? activity, Exception exception)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag(OperationOutcomeAttribute, FailureOutcome);
        activity.SetTag(ErrorTypeAttribute, GetTypeIdentity(exception.GetType()));
        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.AddException(exception);
    }

    internal static void CompleteAsCanceled(Activity? activity)
    {
        activity?.SetTag(OperationOutcomeAttribute, CanceledOutcome);
    }

    private static Activity? StartOperation(
        Type operationType,
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

        return activity;
    }

    private static string GetTypeIdentity(Type type)
    {
        return type.FullName ?? type.Name;
    }
}
