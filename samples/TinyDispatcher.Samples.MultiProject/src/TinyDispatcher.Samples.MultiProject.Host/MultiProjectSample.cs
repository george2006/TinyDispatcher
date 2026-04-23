using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using TinyDispatcher.Samples.MultiProject.Billing;
using TinyDispatcher.Samples.MultiProject.Orders;
using TinyAppContext = TinyDispatcher.AppContext;

namespace TinyDispatcher.Samples.MultiProject.Host;

internal static class MultiProjectSample
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.AddTransient(typeof(ConsoleTracingMiddleware<,>));

        services.UseTinyDispatcher<TinyAppContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ConsoleTracingMiddleware<,>));
        });

        await using var serviceProvider = services.BuildServiceProvider();

        var dispatcher = serviceProvider.GetRequiredService<IDispatcher<TinyAppContext>>();

        Console.WriteLine("Multi-project handler discovery sample");
        Console.WriteLine("=====================================");
        Console.WriteLine("Host project bootstraps Tiny once.");
        Console.WriteLine("Orders and Billing handlers live in separate class libraries.");
        Console.WriteLine();

        await dispatcher.DispatchAsync(new CreateOrder("ORD-100", 49.95m), CancellationToken.None);
        Console.WriteLine();
        await dispatcher.DispatchAsync(new CapturePayment("PAY-200", 49.95m), CancellationToken.None);
    }
}

internal sealed class ConsoleTracingMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Middleware] -> {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        Console.WriteLine($"[Middleware] <- {typeof(TCommand).Name}");
    }
}
