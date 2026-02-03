#nullable enable
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using static TinyDispatcher.Samples.ShippedContext.AppContextShippedFeature;

namespace TinyDispatcher.Samples.ShippedContext;

public sealed class AppContextShippedFeature
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ConsoleLoggingMiddleware<,>));
            tiny.AddFeatureInitializer<RequestInfoFeatureInitializer>();
        });
        
        services.AddTransient(typeof(ConsoleLoggingMiddleware<,>));
 
        await using var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher<AppContext>>();

        await dispatcher.DispatchAsync(
            new AuthorizePayment("O-100", 49.99m, "EUR", "paypal"),
            CancellationToken.None);
    }

    // ---- Sample-local types ----

    public sealed record RequestInfoFeature(string CorrelationId);

    public sealed class RequestInfoFeatureInitializer : IFeatureInitializer
    {
        public void Initialize(IFeatureCollection features)
            => features.Add(new RequestInfoFeature(Guid.NewGuid().ToString("n")));
    }

    public sealed record AuthorizePayment(
        string OrderId,
        decimal Amount,
        string Currency,
        string PaymentMethod) : ICommand;

    public sealed class AuthorizePaymentHandler : ICommandHandler<AuthorizePayment, AppContext>
    {
        public Task HandleAsync(AuthorizePayment cmd, AppContext ctx, CancellationToken ct = default)
        {
            var req = ctx.GetFeature<RequestInfoFeature>();
            Console.WriteLine($"[HANDLER:A] corr={req.CorrelationId} order={cmd.OrderId} {cmd.Amount} {cmd.Currency} pm={cmd.PaymentMethod}");
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
            Console.WriteLine($"[MW] -> {typeof(ConsoleLoggingMiddleware<,>).Name}");
            await next(command, ctx, ct);
            Console.WriteLine($"[MW] <- {typeof(ConsoleLoggingMiddleware<,>).Name}");
        }
    }
}
