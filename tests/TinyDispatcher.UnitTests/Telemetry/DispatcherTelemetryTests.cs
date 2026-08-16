using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTests.Telemetry;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DispatcherTelemetryTestCollection
{
    public const string Name = "Dispatcher telemetry";
}

[Collection(DispatcherTelemetryTestCollection.Name)]
public sealed class DispatcherTelemetryTests
{
    [Fact]
    public async Task Command_dispatch_emits_an_operation_activity()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand, TelemetryContext>, TestCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        await dispatcher.DispatchAsync(new TestCommand("secret-payload"));

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal(nameof(TestCommand), activity.OperationName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(nameof(TestCommand), GetTag(activity, "tiny.operation.name"));
        Assert.Equal(typeof(TestCommand).FullName, GetTag(activity, "tiny.operation.identity"));
        Assert.Equal("command", GetTag(activity, "tiny.operation.type"));
        Assert.Equal(typeof(TestCommandHandler).FullName, GetTag(activity, "tiny.operation.handler"));
        Assert.Equal("success", GetTag(activity, "tiny.operation.outcome"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Value == "secret-payload");
    }

    [Fact]
    public async Task Query_dispatch_emits_an_operation_activity()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IQueryHandler<TestQuery, string>, TestQueryHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        var result = await dispatcher.DispatchAsync<TestQuery, string>(new TestQuery("secret-query"));

        Assert.Equal("query-result", result);

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal(nameof(TestQuery), activity.OperationName);
        Assert.Equal("query", GetTag(activity, "tiny.operation.type"));
        Assert.Equal(typeof(TestQueryHandler).FullName, GetTag(activity, "tiny.operation.handler"));
        Assert.Equal("success", GetTag(activity, "tiny.operation.outcome"));
        Assert.DoesNotContain(activity.Tags, tag => tag.Value == "secret-query");
    }

    [Fact]
    public async Task Existing_activity_is_the_parent_of_the_operation()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand, TelemetryContext>, TestCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        using var parent = new Activity("external-request")
            .SetIdFormat(ActivityIdFormat.W3C)
            .Start();

        await dispatcher.DispatchAsync(new TestCommand("value"));

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.Equal(parent.SpanId, activity.ParentSpanId);
        Assert.Same(parent, Activity.Current);
    }

    [Fact]
    public async Task Nested_dispatch_creates_a_child_operation()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<OuterCommand, TelemetryContext>, OuterCommandHandler>();
            services.AddSingleton<ICommandHandler<InnerCommand, TelemetryContext>, InnerCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        await dispatcher.DispatchAsync(new OuterCommand());

        var outer = Assert.Single(activities.Stopped, activity => activity.OperationName == nameof(OuterCommand));
        var inner = Assert.Single(activities.Stopped, activity => activity.OperationName == nameof(InnerCommand));

        Assert.Equal(outer.TraceId, inner.TraceId);
        Assert.Equal(outer.SpanId, inner.ParentSpanId);
    }

    [Fact]
    public async Task Pipeline_and_handler_observe_the_operation_as_current()
    {
        using var activities = new CapturedActivities();
        var observations = new ActivityObservations();

        using var provider = BuildProvider(services =>
        {
            services.AddSingleton(observations);
            services.AddSingleton<ICommandHandler<PipelineCommand, TelemetryContext>, PipelineCommandHandler>();
            services.AddSingleton<ICommandPipeline<PipelineCommand, TelemetryContext>, ObservingPipeline>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        await dispatcher.DispatchAsync(new PipelineCommand());

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal(activity.SpanId, observations.PipelineSpanId);
        Assert.Equal(activity.SpanId, observations.HandlerSpanId);
    }

    [Fact]
    public async Task Command_without_a_pipeline_is_instrumented()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand, TelemetryContext>, TestCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        await dispatcher.DispatchAsync(new TestCommand("value"));

        Assert.Single(activities.Stopped);
    }

    [Fact]
    public async Task Dispatch_without_a_listener_preserves_the_existing_activity()
    {
        var observations = new ActivityObservations();

        using var provider = BuildProvider(services =>
        {
            services.AddSingleton(observations);
            services.AddSingleton<ICommandHandler<ObservedCommand, TelemetryContext>, ObservedCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        using var parent = new Activity("external-request").Start();

        await dispatcher.DispatchAsync(new ObservedCommand());

        Assert.Equal(parent.SpanId, observations.HandlerSpanId);
        Assert.Same(parent, Activity.Current);
    }

    [Fact]
    public async Task Concurrent_dispatches_keep_independent_operation_activities()
    {
        using var activities = new CapturedActivities();
        var observations = new ConcurrentActivityObservations();

        using var provider = BuildProvider(services =>
        {
            services.AddSingleton(observations);
            services.AddSingleton<ICommandHandler<ConcurrentCommand, TelemetryContext>, ConcurrentCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TelemetryContext>>();

        await Task.WhenAll(
            dispatcher.DispatchAsync(new ConcurrentCommand(1)),
            dispatcher.DispatchAsync(new ConcurrentCommand(2)));

        Assert.Equal(2, activities.Stopped.Count);
        Assert.Equal(2, observations.SpanIds.Count);
        Assert.NotEqual(observations.SpanIds[1], observations.SpanIds[2]);
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IContextFactory<TelemetryContext>, TelemetryContextFactory>();
        services.AddSingleton<IDispatcher<TelemetryContext>>(provider =>
            new Dispatcher<TelemetryContext>(
                provider,
                provider.GetRequiredService<IContextFactory<TelemetryContext>>()));

        configure(services);

        return services.BuildServiceProvider();
    }

    private static string? GetTag(Activity activity, string name)
    {
        return activity.GetTagItem(name)?.ToString();
    }

    private sealed record TestCommand(string Value) : ICommand;

    private sealed class TestCommandHandler : ICommandHandler<TestCommand, TelemetryContext>
    {
        public Task HandleAsync(
            TestCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record TestQuery(string Value) : IQuery<string>;

    private sealed class TestQueryHandler : IQueryHandler<TestQuery, string>
    {
        public Task<string> HandleAsync(TestQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("query-result");
        }
    }

    private sealed record OuterCommand : ICommand;

    private sealed class OuterCommandHandler : ICommandHandler<OuterCommand, TelemetryContext>
    {
        private readonly IDispatcher<TelemetryContext> _dispatcher;

        public OuterCommandHandler(IDispatcher<TelemetryContext> dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public Task HandleAsync(
            OuterCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            return _dispatcher.DispatchAsync(new InnerCommand(), cancellationToken);
        }
    }

    private sealed record InnerCommand : ICommand;

    private sealed class InnerCommandHandler : ICommandHandler<InnerCommand, TelemetryContext>
    {
        public Task HandleAsync(
            InnerCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed record PipelineCommand : ICommand;

    private sealed class PipelineCommandHandler : ICommandHandler<PipelineCommand, TelemetryContext>
    {
        private readonly ActivityObservations _observations;

        public PipelineCommandHandler(ActivityObservations observations)
        {
            _observations = observations;
        }

        public Task HandleAsync(
            PipelineCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            _observations.HandlerSpanId = Activity.Current?.SpanId;
            return Task.CompletedTask;
        }
    }

    private sealed class ObservingPipeline : ICommandPipeline<PipelineCommand, TelemetryContext>
    {
        private readonly ActivityObservations _observations;

        public ObservingPipeline(ActivityObservations observations)
        {
            _observations = observations;
        }

        public async ValueTask ExecuteAsync(
            PipelineCommand command,
            TelemetryContext context,
            ICommandHandler<PipelineCommand, TelemetryContext> handler,
            CancellationToken cancellationToken = default)
        {
            _observations.PipelineSpanId = Activity.Current?.SpanId;
            await handler.HandleAsync(command, context, cancellationToken);
        }
    }

    private sealed record ObservedCommand : ICommand;

    private sealed class ObservedCommandHandler : ICommandHandler<ObservedCommand, TelemetryContext>
    {
        private readonly ActivityObservations _observations;

        public ObservedCommandHandler(ActivityObservations observations)
        {
            _observations = observations;
        }

        public Task HandleAsync(
            ObservedCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            _observations.HandlerSpanId = Activity.Current?.SpanId;
            return Task.CompletedTask;
        }
    }

    private sealed record ConcurrentCommand(int Id) : ICommand;

    private sealed class ConcurrentCommandHandler : ICommandHandler<ConcurrentCommand, TelemetryContext>
    {
        private readonly ConcurrentActivityObservations _observations;

        public ConcurrentCommandHandler(ConcurrentActivityObservations observations)
        {
            _observations = observations;
        }

        public async Task HandleAsync(
            ConcurrentCommand command,
            TelemetryContext context,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            _observations.SpanIds[command.Id] = Activity.Current?.SpanId ?? default;
        }
    }

    private sealed class TelemetryContext
    {
    }

    private sealed class TelemetryContextFactory : IContextFactory<TelemetryContext>
    {
        public ValueTask<TelemetryContext> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new TelemetryContext());
        }
    }

    private sealed class ActivityObservations
    {
        public ActivitySpanId? PipelineSpanId { get; set; }

        public ActivitySpanId? HandlerSpanId { get; set; }
    }

    private sealed class ConcurrentActivityObservations
    {
        public ConcurrentDictionary<int, ActivitySpanId> SpanIds { get; } = new();
    }

    private sealed class CapturedActivities : IDisposable
    {
        private readonly ActivityListener _listener;

        public CapturedActivities()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source =>
                    source.Name == TinyDispatcherTelemetry.ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => Stopped.Enqueue(activity)
            };

            ActivitySource.AddActivityListener(_listener);
        }

        public ConcurrentQueue<Activity> Stopped { get; } = new();

        public void Dispose()
        {
            _listener.Dispose();
        }
    }
}
