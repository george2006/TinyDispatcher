using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples;

public static class PolicySample
{
    public static async Task Run()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            // Policy defines: which commands belong + which middlewares apply
            tiny.UsePolicy<CheckoutPolicy>();
        });

        services.AddTransient(typeof(PolicyLoggingMiddleware<,>));
        services.AddTransient(typeof(PolicyValidationMiddleware<,>));


        var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher<AppContext>>();

        Console.WriteLine("=== Policy sample ===");
        await dispatcher.DispatchAsync(new CreateOrder("ORDER-123"), CancellationToken.None);
        await dispatcher.DispatchAsync(new CancelOrder("ORDER-123"), CancellationToken.None);
        Console.WriteLine("=====================");
    }

    // -----------------------
    // Commands
    // -----------------------

    public sealed record CreateOrder(string OrderId) : ICommand;

    public sealed record CancelOrder(string OrderId) : ICommand;

    // -----------------------
    // Handlers
    // -----------------------

    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
    {
        public Task HandleAsync(CreateOrder command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] CreateOrder: {command.OrderId}");
            return Task.CompletedTask;
        }
    }

    public sealed class CancelOrderHandler : ICommandHandler<CancelOrder, AppContext>
    {
        public Task HandleAsync(CancelOrder command, AppContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine($"[Handler] CancelOrder: {command.OrderId}");
            return Task.CompletedTask;
        }
    }

    // -----------------------
    // Policy
    // -----------------------

    [TinyPolicy]
    [UseMiddleware(typeof(PolicyLoggingMiddleware<,>))]
    [UseMiddleware(typeof(PolicyValidationMiddleware<,>))]
    [ForCommand(typeof(CreateOrder))]
    [ForCommand(typeof(CancelOrder))]
    public sealed class CheckoutPolicy { }

    // -----------------------
    // Middlewares used by policy
    // -----------------------

    public sealed class PolicyLoggingMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            Console.WriteLine($"[PolicyLogging] -> {typeof(TCommand).Name}");
            await next(command, ctx, ct);
            Console.WriteLine($"[PolicyLogging] <- {typeof(TCommand).Name}");
        }
    }

    public sealed class PolicyValidationMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            Console.WriteLine($"[PolicyValidation] OK for {typeof(TCommand).Name}");
            await next(command, ctx, ct);
        }
    }
}
