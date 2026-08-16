using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TinyDispatcher;

internal static class DispatcherTelemetry
{
    private const string OperationNameAttribute = "tiny.operation.name";
    private const string OperationIdentityAttribute = "tiny.operation.identity";
    private const string OperationTypeAttribute = "tiny.operation.type";
    private const string OperationHandlerAttribute = "tiny.operation.handler";
    private const string OperationOutcomeAttribute = "tiny.operation.outcome";
    private const string DispatcherContextAttribute = "tiny.dispatcher.context";
    private const string ErrorTypeAttribute = "error.type";

    private const string CommandOperationType = "command";
    private const string QueryOperationType = "query";
    private const string SuccessOutcome = "success";
    private const string FailureOutcome = "failure";
    private const string CanceledOutcome = "canceled";

    private static readonly ActivitySource ActivitySource = new(
        TinyDispatcherTelemetry.ActivitySourceName,
        typeof(TinyDispatcherTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Meter Meter = new(
        TinyDispatcherTelemetry.MeterName,
        typeof(TinyDispatcherTelemetry).Assembly.GetName().Version?.ToString());

    private static readonly Counter<long> OperationExecutions = Meter.CreateCounter<long>(
        "tiny.operation.executions",
        "{operation}",
        "Number of completed TinyDispatcher operations.");

    private static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "tiny.operation.duration",
        "s",
        "Duration of TinyDispatcher operations.");

    internal static DispatcherOperationTelemetry StartCommand<TCommand, TContext>()
        where TCommand : ICommand
    {
        return StartOperation(typeof(TCommand), typeof(TContext), CommandOperationType);
    }

    internal static DispatcherOperationTelemetry StartQuery<TQuery, TContext>()
    {
        return StartOperation(typeof(TQuery), typeof(TContext), QueryOperationType);
    }

    private static DispatcherOperationTelemetry StartOperation(
        Type operationType,
        Type contextType,
        string operationKind)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var operationName = operationType.Name;
        var operationIdentity = GetTypeIdentity(operationType);
        var contextIdentity = GetTypeIdentity(contextType);
        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal);

        if (activity is not null)
        {
            activity.SetTag(OperationNameAttribute, operationName);
            activity.SetTag(OperationIdentityAttribute, operationIdentity);
            activity.SetTag(OperationTypeAttribute, operationKind);
            activity.SetTag(DispatcherContextAttribute, contextIdentity);
        }

        return new DispatcherOperationTelemetry(
            activity,
            operationIdentity,
            contextIdentity,
            operationKind,
            startTimestamp);
    }

    private static string GetTypeIdentity(Type type)
    {
        return type.FullName ?? type.Name;
    }

    private static void CompleteMeasurement(
        DispatcherOperationTelemetry operation,
        string outcome)
    {
        TagList tags = default;
        tags.Add(OperationIdentityAttribute, operation.OperationIdentity);
        tags.Add(DispatcherContextAttribute, operation.ContextIdentity);
        tags.Add(OperationTypeAttribute, operation.OperationType);
        tags.Add(OperationOutcomeAttribute, outcome);

        OperationExecutions.Add(1, in tags);

        var elapsedTimestamp = Stopwatch.GetTimestamp() - operation.StartTimestamp;
        var elapsedSeconds = (double)elapsedTimestamp / Stopwatch.Frequency;
        OperationDuration.Record(elapsedSeconds, in tags);
    }

    internal readonly struct DispatcherOperationTelemetry : IDisposable
    {
        private readonly Activity? _activity;

        internal DispatcherOperationTelemetry(
            Activity? activity,
            string operationIdentity,
            string contextIdentity,
            string operationType,
            long startTimestamp)
        {
            _activity = activity;
            OperationIdentity = operationIdentity;
            ContextIdentity = contextIdentity;
            OperationType = operationType;
            StartTimestamp = startTimestamp;
        }

        internal string OperationIdentity { get; }

        internal string ContextIdentity { get; }

        internal string OperationType { get; }

        internal long StartTimestamp { get; }

        internal void SetHandler(Type handlerType)
        {
            _activity?.SetTag(OperationHandlerAttribute, GetTypeIdentity(handlerType));
        }

        internal void CompleteSuccessfully()
        {
            _activity?.SetTag(OperationOutcomeAttribute, SuccessOutcome);
            CompleteMeasurement(this, SuccessOutcome);
        }

        internal void CompleteWithFailure(Exception exception)
        {
            if (_activity is not null)
            {
                _activity.SetTag(OperationOutcomeAttribute, FailureOutcome);
                _activity.SetTag(ErrorTypeAttribute, GetTypeIdentity(exception.GetType()));
                _activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                _activity.AddException(exception);
            }

            CompleteMeasurement(this, FailureOutcome);
        }

        internal void CompleteAsCanceled()
        {
            _activity?.SetTag(OperationOutcomeAttribute, CanceledOutcome);
            CompleteMeasurement(this, CanceledOutcome);
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }
}
