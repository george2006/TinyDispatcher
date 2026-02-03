#nullable enable
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples.CustomContext;
public sealed class CustomContextCallbackFeature
{
    public static async Task RunAsync(CancellationToken ct = default)
    {
        Console.WriteLine("Custom context via UseTinyDispatcher contextFactory callback");
        Console.WriteLine("=========================================================");
        Console.WriteLine();

        var services = new ServiceCollection();

        services.AddTransient(typeof(ConsoleLoggingMiddleware<,>));

        // IMPORTANT: UseTinyDispatcher is meant to be called once per assembly.
        // This sample is isolated in its own project on purpose.
        services.UseTinyDispatcher<PaymentsContext>(
            configure: tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(ConsoleLoggingMiddleware<,>));
            },
            contextFactory: (sp, ct2) =>
            {
                // In ASP.NET this would be built from HttpContext (traceparent, headers, user, etc.)
                var ctx = new PaymentsContext(
                    CorrelationId: Guid.NewGuid().ToString("n"),
                    IdempotencyKey: "idem-" + Guid.NewGuid().ToString("n"),
                    Caller: "console-callback");

                return ValueTask.FromResult(ctx);
            });

        await using var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetRequiredService<IDispatcher<PaymentsContext>>();

        await dispatcher.DispatchAsync(
            new AuthorizePayment(
                OrderId: "O-200",
                Amount: 19.95m,
                Currency: "EUR",
                PaymentMethod: "applepay"),
            ct);
    }

    // ------------------- Sample types -------------------
    public sealed record PaymentsContext(string CorrelationId, string IdempotencyKey, string Caller);

    public sealed record AuthorizePayment(
        string OrderId,
        decimal Amount,
        string Currency,
        string PaymentMethod
    ) : ICommand;

    public sealed class AuthorizePaymentHandler : ICommandHandler<AuthorizePayment, PaymentsContext>
    {
        public Task HandleAsync(AuthorizePayment cmd, PaymentsContext ctx, CancellationToken ct = default)
        {
            Console.WriteLine(
                $"[HANDLER] corr={ctx.CorrelationId} idem={ctx.IdempotencyKey} caller={ctx.Caller} " +
                $"order={cmd.OrderId} amount={cmd.Amount} {cmd.Currency} pm={cmd.PaymentMethod}");

            return Task.CompletedTask;
        }
    }

    public sealed class ConsoleLoggingMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            Console.WriteLine($"[MW] -> {typeof(TCommand).Name}");
            await next(command, ctx, ct);
            Console.WriteLine($"[MW] <- {typeof(TCommand).Name}");
        }
    }
}
