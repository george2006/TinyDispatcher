using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;


namespace TinyDispatcher.UnitTests.NoOp;

public sealed class NoOpContextDispatchTests
{
    [Fact]
    public async Task Dispatch_with_NoOpContext_calls_handler_and_middlewares()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<NoOpCallTracker>();
        NoOpTestBootstrap.Configure(services);
        using var sp = services.BuildServiceProvider();
       
        var dispatcher = sp.GetRequiredService<IDispatcher<NoOpContext>>();

        // Act
        await dispatcher.DispatchAsync(new NoOpTestCommand("hello"));

        // Assert
        var tracker = sp.GetRequiredService<NoOpCallTracker>();
        Assert.True(tracker.GlobalMiddlewareCalled);
        Assert.True(tracker.PerCommandMiddlewareCalled);
        Assert.True(tracker.HandlerCalled);
    }

    [Fact]
    public async Task Dispatch_other_command_with_NoOpContext_does_not_require_percommand_middleware()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<NoOpCallTracker>();
        NoOpTestBootstrap.Configure(services);
        using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<NoOpContext>>();

        // Act
        await dispatcher.DispatchAsync(new NoOpOtherCommand("x"));

        // Assert
        var tracker = sp.GetRequiredService<NoOpCallTracker>();
        Assert.True(tracker.GlobalMiddlewareCalled);
        Assert.False(tracker.PerCommandMiddlewareCalled); // per-command middleware only for NoOpTestCommand
        Assert.False(tracker.HandlerCalled);              // handler flag only set by NoOpTestHandler
    }
}