#nullable enable
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.Samples.CustomContextFactory;

public sealed class CustomContextFactoryFeature
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("Custom context via DI-registered IContextFactory<TContext>");
        Console.WriteLine("========================================================");
        Console.WriteLine();

        var services = new ServiceCollection();
        services.AddTransient(typeof(ConsoleLoggingMiddleware<,>));

        // Register the context factory in DI (this is the key difference vs callback)
        services.AddScoped<IContextFactory<PaymentsContext>, PaymentsContextFactory>();

        // IMPORTANT: UseTinyDispatcher is called once per assembly.
        // No contextFactory callback here — Tiny will resolve IContextFactory<TContext> from DI.
        services.UseTinyDispatcher<PaymentsContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ConsoleLoggingMiddleware<,>));
        });

        await using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher<PaymentsContext>>();

        await dispatcher.DispatchAsync(
            new AuthorizePayment(
                OrderId: "O-300",
                Amount: 120.00m,
                Currency: "EUR",
                PaymentMethod: "card"),
            ct);
    }

    // ------------------- Sample types -------------------

    public sealed record PaymentsContext(
        string CorrelationId,
        string IdempotencyKey,
        string Caller);

    public sealed class PaymentsContextFactory : IContextFactory<PaymentsContext>
    {
        public ValueTask<PaymentsContext> CreateAsync(CancellationToken ct = default)
        {
            var ctx = new PaymentsContext(
                CorrelationId: Guid.NewGuid().ToString("n"),
                IdempotencyKey: "idem-" + Guid.NewGuid().ToString("n"),
                Caller: "console-factory");

            return ValueTask.FromResult(ctx);
        }
    }

    public sealed record AuthorizePayment(
        string OrderId,
        decimal Amount,
        string Currency,
        string PaymentMethod
    ) : ICommand;

    public sealed class AuthorizePaymentHandler
        : ICommandHandler<AuthorizePayment, PaymentsContext>
    {
        public Task HandleAsync(
            AuthorizePayment cmd,
            PaymentsContext ctx,
            CancellationToken ct = default)
        {
            Console.WriteLine(
                $"[HANDLER] corr={ctx.CorrelationId} idem={ctx.IdempotencyKey} caller={ctx.Caller} " +
                $"order={cmd.OrderId} amount={cmd.Amount} {cmd.Currency} pm={cmd.PaymentMethod}");

            return Task.CompletedTask;
        }
    }

    public sealed class ConsoleLoggingMiddleware<TCommand, TContext>
        : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct = default)
        {
            Console.WriteLine($"[MW] -> {typeof(TCommand).Name}");
            await runtime.NextAsync(command, ctx, ct).ConfigureAwait(false);
            Console.WriteLine($"[MW] <- {typeof(TCommand).Name}");
        }
    }
}
