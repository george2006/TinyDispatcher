using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.IntegrationTests.Dispatching;

public sealed class DispatcherRuntimeTestDomain
{
    [Fact]
    public async Task Dispatch_when_pipeline_is_registered_executes_pipeline_and_handler()
    {
        var tracker = new CallTracker();

        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddSingleton(new TestContext());
        services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
        services.AddScoped<ICommandHandler<TestCommand, TestContext>, TestHandler>();
        services.AddScoped<ICommandPipeline<TestCommand, TestContext>, CommandPipeline>();
        services.AddScoped<IDispatcher<TestContext>, Dispatcher<TestContext>>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new TestCommand("hello"));

        Assert.True(tracker.PipelineCalled);
        Assert.True(tracker.HandlerCalled);
    }

    [Fact]
    public async Task Dispatch_when_pipeline_is_not_registered_executes_handler_directly()
    {
        var tracker = new CallTracker();

        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddSingleton(new TestContext());
        services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
        services.AddScoped<ICommandHandler<TestCommand, TestContext>, TestHandler>();
        services.AddScoped<IDispatcher<TestContext>, Dispatcher<TestContext>>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new TestCommand("hello"));

        Assert.False(tracker.PipelineCalled);
        Assert.True(tracker.HandlerCalled);
    }

    [Fact]
    public async Task Dispatch_propagates_cancellation_token_to_context_factory_and_handler()
    {
        var tracker = new CallTracker();
        var probe = new TestContext();

        var services = new ServiceCollection();
        services.AddSingleton(tracker);
        services.AddSingleton(probe);
        services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
        services.AddScoped<ICommandHandler<TestCommand, TestContext>, TestHandler>();
        services.AddScoped<IDispatcher<TestContext>, Dispatcher<TestContext>>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher<TestContext>>();
        var cancellationToken = new CancellationTokenSource().Token;

        await dispatcher.DispatchAsync(new TestCommand("hello"), cancellationToken);

        Assert.NotNull(probe.LastCreatedContext);
        Assert.Equal(cancellationToken, probe.LastCreatedContext!.SeenByFactory);
        Assert.Equal(cancellationToken, probe.LastCreatedContext.SeenByHandler);
    }

}