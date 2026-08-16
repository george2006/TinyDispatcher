using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTests.Telemetry;

[Collection(DispatcherTelemetryTestCollection.Name)]
public sealed class DispatcherOpenTelemetryTests
{
    [Fact]
    public async Task Ordinary_tracer_provider_exports_resource_parent_and_nested_operations()
    {
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                serviceName: "Payments",
                serviceVersion: "1.2.3"))
            .SetSampler(new AlwaysOnSampler())
            .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
            .AddInMemoryExporter(exportedActivities)
            .Build();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<OuterCommand, TestContext>, OuterCommandHandler>();
            services.AddSingleton<ICommandHandler<InnerCommand, TestContext>, InnerCommandHandler>();
        });

        using var externalParent = new Activity("POST /payments").Start();
        await provider.GetRequiredService<IDispatcher<TestContext>>().DispatchAsync(new OuterCommand());
        externalParent.Stop();
        tracerProvider.ForceFlush();

        Assert.Equal("Payments", GetResourceValue(tracerProvider, "service.name"));
        Assert.Equal("1.2.3", GetResourceValue(tracerProvider, "service.version"));

        var outer = Assert.Single(exportedActivities, activity => activity.OperationName == nameof(OuterCommand));
        var inner = Assert.Single(exportedActivities, activity => activity.OperationName == nameof(InnerCommand));

        Assert.Equal(externalParent.SpanId, outer.ParentSpanId);
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
        Assert.Equal(outer.TraceId, inner.TraceId);
        Assert.Equal("success", outer.GetTagItem("tiny.operation.outcome"));
        Assert.Equal("success", inner.GetTagItem("tiny.operation.outcome"));
    }

    [Fact]
    public async Task Ordinary_tracer_provider_exports_failure_and_cancellation_semantics()
    {
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
            .AddInMemoryExporter(exportedActivities)
            .Build();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<FailingCommand, TestContext>, FailingCommandHandler>();
            services.AddSingleton<ICommandHandler<CanceledCommand, TestContext>, CanceledCommandHandler>();
        });
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAsync<TestException>(() => dispatcher.DispatchAsync(new FailingCommand()));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new CanceledCommand(), cancellation.Token));
        tracerProvider.ForceFlush();

        var failure = Assert.Single(exportedActivities, activity => activity.OperationName == nameof(FailingCommand));
        Assert.Equal(ActivityStatusCode.Error, failure.Status);
        Assert.Equal("failure", failure.GetTagItem("tiny.operation.outcome"));
        Assert.Equal(typeof(TestException).FullName, failure.GetTagItem("error.type"));
        Assert.Equal("exception", Assert.Single(failure.Events).Name);

        var canceled = Assert.Single(exportedActivities, activity => activity.OperationName == nameof(CanceledCommand));
        Assert.Equal(ActivityStatusCode.Unset, canceled.Status);
        Assert.Equal("canceled", canceled.GetTagItem("tiny.operation.outcome"));
        Assert.Empty(canceled.Events);
    }

    [Fact]
    public async Task Metrics_remain_complete_when_the_tracer_drops_every_operation()
    {
        var exportedActivities = new List<Activity>();
        var exportedMetrics = new List<Metric>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(new AlwaysOffSampler())
            .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
            .AddInMemoryExporter(exportedActivities)
            .Build();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(TinyDispatcherTelemetry.MeterName)
            .AddInMemoryExporter(exportedMetrics)
            .Build();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<SuccessfulCommand, TestContext>, SuccessfulCommandHandler>();
            services.AddSingleton<ICommandHandler<FailingCommand, TestContext>, FailingCommandHandler>();
        });
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new SuccessfulCommand());
        await Assert.ThrowsAsync<TestException>(() => dispatcher.DispatchAsync(new FailingCommand()));
        tracerProvider.ForceFlush();
        meterProvider.ForceFlush();

        Assert.Empty(exportedActivities);

        var executionMetric = Assert.Single(exportedMetrics, metric => metric.Name == "tiny.operation.executions");
        var durationMetric = Assert.Single(exportedMetrics, metric => metric.Name == "tiny.operation.duration");
        var executions = ReadPoints(executionMetric);
        var durations = ReadPoints(durationMetric);

        Assert.Equal(2, executions.Count);
        Assert.Equal(2, durations.Count);
        Assert.Equal(2, executions.Sum(point => point.Value));
        Assert.All(durations, point => Assert.Equal(1, point.Count));
        Assert.Equal(new[] { "failure", "success" },
            executions.Select(point => point.Tags["tiny.operation.outcome"]).OrderBy(value => value));
        Assert.All(executions, point => Assert.Equal(
            new[] { "tiny.dispatcher.context", "tiny.operation.identity", "tiny.operation.outcome", "tiny.operation.type" },
            point.Tags.Keys.OrderBy(key => key)));
        Assert.All(executions, point => Assert.Equal(
            typeof(TestContext).FullName,
            point.Tags["tiny.dispatcher.context"]));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContextFactory<TestContext>, TestContextFactory>();
        services.AddSingleton<IDispatcher<TestContext>>(provider =>
            new Dispatcher<TestContext>(provider, provider.GetRequiredService<IContextFactory<TestContext>>()));
        configure(services);
        return services.BuildServiceProvider();
    }

    private static string? GetResourceValue(BaseProvider provider, string key)
    {
        return provider.GetResource().Attributes
            .Single(attribute => attribute.Key == key)
            .Value?.ToString();
    }

    private static List<ExportedMetricPoint> ReadPoints(Metric metric)
    {
        var points = new List<ExportedMetricPoint>();

        foreach (ref readonly var point in metric.GetMetricPoints())
        {
            var tags = new Dictionary<string, string?>();
            foreach (var tag in point.Tags)
            {
                tags[tag.Key] = tag.Value?.ToString();
            }

            if (metric.Name == "tiny.operation.executions")
            {
                points.Add(new ExportedMetricPoint(tags, point.GetSumLong(), 0));
            }
            else
            {
                points.Add(new ExportedMetricPoint(tags, 0, point.GetHistogramCount()));
            }
        }

        return points;
    }

    private sealed record ExportedMetricPoint(
        IReadOnlyDictionary<string, string?> Tags,
        long Value,
        long Count);

    private sealed record OuterCommand : ICommand;

    private sealed class OuterCommandHandler : ICommandHandler<OuterCommand, TestContext>
    {
        private readonly IDispatcher<TestContext> _dispatcher;

        public OuterCommandHandler(IDispatcher<TestContext> dispatcher) => _dispatcher = dispatcher;

        public async Task HandleAsync(OuterCommand command, TestContext context, CancellationToken cancellationToken = default)
        {
            await _dispatcher.DispatchAsync(new InnerCommand(), cancellationToken);
        }
    }

    private sealed record InnerCommand : ICommand;

    private sealed class InnerCommandHandler : ICommandHandler<InnerCommand, TestContext>
    {
        public Task HandleAsync(InnerCommand command, TestContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record SuccessfulCommand : ICommand;

    private sealed class SuccessfulCommandHandler : ICommandHandler<SuccessfulCommand, TestContext>
    {
        public Task HandleAsync(SuccessfulCommand command, TestContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed record FailingCommand : ICommand;

    private sealed class FailingCommandHandler : ICommandHandler<FailingCommand, TestContext>
    {
        public Task HandleAsync(FailingCommand command, TestContext context, CancellationToken cancellationToken = default)
            => throw new TestException();
    }

    private sealed record CanceledCommand : ICommand;

    private sealed class CanceledCommandHandler : ICommandHandler<CanceledCommand, TestContext>
    {
        public Task HandleAsync(CanceledCommand command, TestContext context, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }

    private sealed class TestContext;

    private sealed class TestContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new TestContext());
    }

    private sealed class TestException : Exception;
}
