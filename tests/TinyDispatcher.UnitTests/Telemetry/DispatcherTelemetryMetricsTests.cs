using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTests.Telemetry;

[Collection(DispatcherTelemetryTestCollection.Name)]
public sealed class DispatcherTelemetryMetricsTests
{
    [Fact]
    public async Task Successful_command_records_one_execution_and_duration_without_an_activity_listener()
    {
        using var metrics = new CapturedMetrics();
        using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<TestCommand, TestContext>, SuccessfulCommandHandler>());

        await provider.GetRequiredService<IDispatcher<TestContext>>()
            .DispatchAsync(new TestCommand("secret-value"));

        var execution = Assert.Single(metrics.Executions);
        var duration = Assert.Single(metrics.Durations);

        Assert.Equal(1, execution.LongValue);
        Assert.Equal("tiny.operation.executions", execution.InstrumentName);
        Assert.Equal("tiny.operation.duration", duration.InstrumentName);
        Assert.Equal("{operation}", metrics.Instruments[execution.InstrumentName]);
        Assert.Equal("s", metrics.Instruments[duration.InstrumentName]);
        Assert.Equal(typeof(TestCommand).FullName, execution.Tags["tiny.operation.identity"]);
        Assert.Equal("command", execution.Tags["tiny.operation.type"]);
        Assert.Equal("success", execution.Tags["tiny.operation.outcome"]);
        Assert.Equal(execution.Tags, duration.Tags);
        Assert.True(duration.DoubleValue >= 0);
        Assert.Equal(
            new[] { "tiny.operation.identity", "tiny.operation.outcome", "tiny.operation.type" },
            execution.Tags.Keys.OrderBy(key => key));
        Assert.DoesNotContain(execution.Tags.Values, value => value == "secret-value");
    }

    [Fact]
    public async Task Successful_query_records_one_execution_and_duration()
    {
        using var metrics = new CapturedMetrics();
        using var provider = BuildProvider(services =>
            services.AddSingleton<IQueryHandler<TestQuery, string>, SuccessfulQueryHandler>());

        var result = await provider.GetRequiredService<IDispatcher<TestContext>>()
            .DispatchAsync<TestQuery, string>(new TestQuery());

        Assert.Equal("result", result);

        var execution = Assert.Single(metrics.Executions);
        Assert.Single(metrics.Durations);
        Assert.Equal(typeof(TestQuery).FullName, execution.Tags["tiny.operation.identity"]);
        Assert.Equal("query", execution.Tags["tiny.operation.type"]);
        Assert.Equal("success", execution.Tags["tiny.operation.outcome"]);
    }

    [Fact]
    public async Task Command_and_query_failures_are_recorded_once()
    {
        using var metrics = new CapturedMetrics();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<FailingCommand, TestContext>, FailingCommandHandler>();
            services.AddSingleton<IQueryHandler<FailingQuery, string>, FailingQueryHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAsync<TestException>(() => dispatcher.DispatchAsync(new FailingCommand()));
        await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync<FailingQuery, string>(new FailingQuery()));

        Assert.Equal(2, metrics.Executions.Count);
        Assert.Equal(2, metrics.Durations.Count);
        Assert.All(metrics.Executions, measurement =>
            Assert.Equal("failure", measurement.Tags["tiny.operation.outcome"]));
    }

    [Fact]
    public async Task Requested_cancellation_is_canceled_and_unrequested_cancellation_is_failure()
    {
        using var metrics = new CapturedMetrics();
        using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<CanceledCommand, TestContext>, CanceledCommandHandler>());
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new CanceledCommand(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new CanceledCommand(), CancellationToken.None));

        Assert.Equal(2, metrics.Executions.Count);
        Assert.Equal(2, metrics.Durations.Count);
        Assert.Equal(
            new[] { "canceled", "failure" },
            metrics.Executions.Select(item => item.Tags["tiny.operation.outcome"]));
    }

    [Fact]
    public async Task Activity_sampling_does_not_change_metric_counts()
    {
        using var metrics = new CapturedMetrics();
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TinyDispatcherTelemetry.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.None
        };
        ActivitySource.AddActivityListener(activityListener);

        using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<TestCommand, TestContext>, SuccessfulCommandHandler>());
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new TestCommand("one"));
        await dispatcher.DispatchAsync(new TestCommand("two"));

        Assert.Equal(2, metrics.Executions.Count);
        Assert.Equal(2, metrics.Durations.Count);
    }

    [Fact]
    public async Task Concurrent_dispatches_record_independent_measurements()
    {
        const int dispatchCount = 20;
        using var metrics = new CapturedMetrics();
        using var provider = BuildProvider(services =>
            services.AddSingleton<ICommandHandler<ConcurrentCommand, TestContext>, ConcurrentCommandHandler>());
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Task.WhenAll(Enumerable.Range(0, dispatchCount)
            .Select(id => dispatcher.DispatchAsync(new ConcurrentCommand(id))));

        Assert.Equal(dispatchCount, metrics.Executions.Count);
        Assert.Equal(dispatchCount, metrics.Durations.Count);
        Assert.All(metrics.Executions, measurement => Assert.Equal(1, measurement.LongValue));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContextFactory<TestContext>, TestContextFactory>();
        services.AddSingleton<IDispatcher<TestContext>>(provider =>
            new Dispatcher<TestContext>(
                provider,
                provider.GetRequiredService<IContextFactory<TestContext>>()));
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed record TestCommand(string Secret) : ICommand;

    private sealed class SuccessfulCommandHandler : ICommandHandler<TestCommand, TestContext>
    {
        public Task HandleAsync(TestCommand command, TestContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record TestQuery : IQuery<string>;

    private sealed class SuccessfulQueryHandler : IQueryHandler<TestQuery, string>
    {
        public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult("result");
    }

    private sealed record FailingCommand : ICommand;

    private sealed class FailingCommandHandler : ICommandHandler<FailingCommand, TestContext>
    {
        public Task HandleAsync(FailingCommand command, TestContext context, CancellationToken cancellationToken = default)
            => throw new TestException();
    }

    private sealed record FailingQuery : IQuery<string>;

    private sealed class FailingQueryHandler : IQueryHandler<FailingQuery, string>
    {
        public Task<string> HandleAsync(FailingQuery query, CancellationToken cancellationToken = default)
            => throw new TestException();
    }

    private sealed record CanceledCommand : ICommand;

    private sealed class CanceledCommandHandler : ICommandHandler<CanceledCommand, TestContext>
    {
        public Task HandleAsync(CanceledCommand command, TestContext context, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed record ConcurrentCommand(int Id) : ICommand;

    private sealed class ConcurrentCommandHandler : ICommandHandler<ConcurrentCommand, TestContext>
    {
        public async Task HandleAsync(ConcurrentCommand command, TestContext context, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
        }
    }

    private sealed class TestContext;

    private sealed class TestContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new TestContext());
    }

    private sealed class TestException : Exception;

    private sealed class CapturedMetrics : IDisposable
    {
        private readonly MeterListener _listener = new();

        public CapturedMetrics()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == TinyDispatcherTelemetry.MeterName)
                {
                    Instruments[instrument.Name] = instrument.Unit;
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                Executions.Enqueue(MetricMeasurement.From(instrument.Name, value, tags)));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                Durations.Enqueue(MetricMeasurement.From(instrument.Name, value, tags)));
            _listener.Start();
        }

        public ConcurrentQueue<MetricMeasurement> Executions { get; } = new();

        public ConcurrentQueue<MetricMeasurement> Durations { get; } = new();

        public ConcurrentDictionary<string, string?> Instruments { get; } = new();

        public void Dispose() => _listener.Dispose();
    }

    private sealed record MetricMeasurement(
        string InstrumentName,
        long? LongValue,
        double? DoubleValue,
        IReadOnlyDictionary<string, string?> Tags)
    {
        public static MetricMeasurement From<T>(
            string instrumentName,
            T value,
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
            where T : struct
        {
            var copiedTags = tags.ToArray().ToDictionary(tag => tag.Key, tag => tag.Value?.ToString());

            return value switch
            {
                long longValue => new MetricMeasurement(instrumentName, longValue, null, copiedTags),
                double doubleValue => new MetricMeasurement(instrumentName, null, doubleValue, copiedTags),
                _ => throw new InvalidOperationException($"Unsupported measurement type {typeof(T)}.")
            };
        }
    }
}
