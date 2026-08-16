using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTests.Telemetry;

[Collection(DispatcherTelemetryTestCollection.Name)]
public sealed class DispatcherTelemetryOutcomeTests
{
    [Fact]
    public async Task Handler_resolution_failure_marks_the_operation_as_failed()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(new MissingHandlerCommand()));

        var activity = Assert.Single(activities.Stopped);

        AssertFailed(activity, typeof(InvalidOperationException));
        Assert.Null(activity.GetTagItem("tiny.operation.handler"));
    }

    [Fact]
    public async Task Context_creation_failure_marks_the_operation_as_failed()
    {
        var expectedException = new TestException("context failed");
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IContextFactory<TestContext>>(
                new ThrowingContextFactory(expectedException));
            services.AddSingleton<ICommandHandler<TestCommand, TestContext>, SuccessfulCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        var actualException = await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync(new TestCommand()));

        Assert.Same(expectedException, actualException);
        AssertFailed(Assert.Single(activities.Stopped), typeof(TestException), "context failed");
    }

    [Fact]
    public async Task Pipeline_failure_marks_the_operation_as_failed()
    {
        var expectedException = new TestException("pipeline failed");
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand, TestContext>, SuccessfulCommandHandler>();
            services.AddSingleton<ICommandPipeline<TestCommand, TestContext>>(
                new ThrowingPipeline(expectedException));
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        var actualException = await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync(new TestCommand()));

        Assert.Same(expectedException, actualException);
        AssertFailed(Assert.Single(activities.Stopped), typeof(TestException), "pipeline failed");
    }

    [Fact]
    public async Task Command_handler_failure_records_the_standard_exception_event()
    {
        var expectedException = new TestException("handler failed");
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<TestCommand, TestContext>>(
                new ThrowingCommandHandler(expectedException));
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        var actualException = await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync(new TestCommand()));

        Assert.Same(expectedException, actualException);
        AssertFailed(Assert.Single(activities.Stopped), typeof(TestException), "handler failed");
    }

    [Fact]
    public async Task Synchronous_query_failure_marks_the_operation_as_failed()
    {
        var expectedException = new TestException("query failed synchronously");
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IQueryHandler<TestQuery, string>>(
                new SynchronouslyThrowingQueryHandler(expectedException));
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        var actualException = await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync<TestQuery, string>(new TestQuery()));

        Assert.Same(expectedException, actualException);
        AssertFailed(
            Assert.Single(activities.Stopped),
            typeof(TestException),
            "query failed synchronously");
    }

    [Fact]
    public async Task Asynchronous_query_failure_marks_the_operation_as_failed()
    {
        var expectedException = new TestException("query failed asynchronously");
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IQueryHandler<TestQuery, string>>(
                new AsynchronouslyThrowingQueryHandler(expectedException));
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        var actualException = await Assert.ThrowsAsync<TestException>(() =>
            dispatcher.DispatchAsync<TestQuery, string>(new TestQuery()));

        Assert.Same(expectedException, actualException);
        AssertFailed(
            Assert.Single(activities.Stopped),
            typeof(TestException),
            "query failed asynchronously");
    }

    [Fact]
    public async Task Requested_cancellation_marks_the_operation_as_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<CanceledCommand, TestContext>, CanceledCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new CanceledCommand(), cancellation.Token));

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal("canceled", GetTag(activity, "tiny.operation.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Empty(activity.Events);
        Assert.Null(activity.GetTagItem("error.type"));
    }

    [Fact]
    public async Task Unrequested_operation_cancellation_is_an_operation_failure()
    {
        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<ICommandHandler<CanceledCommand, TestContext>, CanceledCommandHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync(new CanceledCommand(), CancellationToken.None));

        AssertFailed(Assert.Single(activities.Stopped), typeof(OperationCanceledException));
    }

    [Fact]
    public async Task Requested_query_cancellation_marks_the_operation_as_canceled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using var activities = new CapturedActivities();
        using var provider = BuildProvider(services =>
        {
            services.AddSingleton<IQueryHandler<CanceledQuery, string>, CanceledQueryHandler>();
        });

        var dispatcher = provider.GetRequiredService<IDispatcher<TestContext>>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            dispatcher.DispatchAsync<CanceledQuery, string>(
                new CanceledQuery(),
                cancellation.Token));

        var activity = Assert.Single(activities.Stopped);

        Assert.Equal("canceled", GetTag(activity, "tiny.operation.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Empty(activity.Events);
        Assert.Null(activity.GetTagItem("error.type"));
    }

    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IContextFactory<TestContext>, SuccessfulContextFactory>();
        services.AddSingleton<IDispatcher<TestContext>>(provider =>
            new Dispatcher<TestContext>(
                provider,
                provider.GetRequiredService<IContextFactory<TestContext>>()));

        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    private static void AssertFailed(
        Activity activity,
        Type exceptionType,
        string? exceptionMessage = null)
    {
        Assert.Equal("failure", GetTag(activity, "tiny.operation.outcome"));
        Assert.Equal(exceptionType.FullName, GetTag(activity, "error.type"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);

        var exceptionEvent = Assert.Single(activity.Events);

        Assert.Equal("exception", exceptionEvent.Name);
        Assert.Equal(exceptionType.FullName, GetEventTag(exceptionEvent, "exception.type"));
        Assert.False(string.IsNullOrWhiteSpace(GetEventTag(exceptionEvent, "exception.stacktrace")));

        if (exceptionMessage is not null)
        {
            Assert.Equal(exceptionMessage, GetEventTag(exceptionEvent, "exception.message"));
        }
    }

    private static string? GetTag(Activity activity, string name)
    {
        return activity.GetTagItem(name)?.ToString();
    }

    private static string? GetEventTag(ActivityEvent activityEvent, string name)
    {
        foreach (var tag in activityEvent.Tags)
        {
            if (tag.Key == name)
            {
                return tag.Value?.ToString();
            }
        }

        return null;
    }

    private sealed record MissingHandlerCommand : ICommand;

    private sealed record TestCommand : ICommand;

    private sealed class SuccessfulCommandHandler : ICommandHandler<TestCommand, TestContext>
    {
        public Task HandleAsync(
            TestCommand command,
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCommandHandler : ICommandHandler<TestCommand, TestContext>
    {
        private readonly Exception _exception;

        public ThrowingCommandHandler(Exception exception)
        {
            _exception = exception;
        }

        public Task HandleAsync(
            TestCommand command,
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class ThrowingPipeline : ICommandPipeline<TestCommand, TestContext>
    {
        private readonly Exception _exception;

        public ThrowingPipeline(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask ExecuteAsync(
            TestCommand command,
            TestContext context,
            ICommandHandler<TestCommand, TestContext> handler,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed record TestQuery : IQuery<string>;

    private sealed class SynchronouslyThrowingQueryHandler : IQueryHandler<TestQuery, string>
    {
        private readonly Exception _exception;

        public SynchronouslyThrowingQueryHandler(Exception exception)
        {
            _exception = exception;
        }

        public Task<string> HandleAsync(
            TestQuery query,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class AsynchronouslyThrowingQueryHandler : IQueryHandler<TestQuery, string>
    {
        private readonly Exception _exception;

        public AsynchronouslyThrowingQueryHandler(Exception exception)
        {
            _exception = exception;
        }

        public async Task<string> HandleAsync(
            TestQuery query,
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            throw _exception;
        }
    }

    private sealed record CanceledCommand : ICommand;

    private sealed class CanceledCommandHandler : ICommandHandler<CanceledCommand, TestContext>
    {
        public Task HandleAsync(
            CanceledCommand command,
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed record CanceledQuery : IQuery<string>;

    private sealed class CanceledQueryHandler : IQueryHandler<CanceledQuery, string>
    {
        public Task<string> HandleAsync(
            CanceledQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class TestContext
    {
    }

    private sealed class SuccessfulContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new TestContext());
        }
    }

    private sealed class ThrowingContextFactory : IContextFactory<TestContext>
    {
        private readonly Exception _exception;

        public ThrowingContextFactory(Exception exception)
        {
            _exception = exception;
        }

        public ValueTask<TestContext> CreateAsync(CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }

    private sealed class TestException : Exception
    {
        public TestException(string message)
            : base(message)
        {
        }
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
