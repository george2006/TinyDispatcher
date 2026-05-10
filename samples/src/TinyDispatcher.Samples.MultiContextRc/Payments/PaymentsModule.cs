using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;
using TinyDispatcher.Samples.MultiContextRc.Shared;

namespace TinyDispatcher.Samples.MultiContextRc.Payments;

public static class PaymentsModule
{
    public static IServiceCollection AddPaymentsContext(this IServiceCollection services)
    {
        services.AddScoped<IContextFactory<PaymentsContext>, PaymentsContextFactory>();
        services.AddTransient(typeof(ConsoleLogMiddleware<,>));
        services.AddTransient(typeof(PaymentAuditMiddleware<,>));
        services.AddTransient(typeof(PaymentPolicyMiddleware<,>));

        services.UseTinyDispatcher<PaymentsContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ConsoleLogMiddleware<,>));
            tiny.UseMiddlewareFor<CapturePayment>(typeof(PaymentAuditMiddleware<,>));
            tiny.UsePolicy<PaymentRiskPolicy>();
        });

        return services;
    }
}

public sealed record CapturePayment(string PaymentId, decimal Amount) : ICommand;
public sealed record RefundPayment(string PaymentId) : ICommand;

public sealed class PaymentsContext
{
    public PaymentsContext(string merchantId, string correlationId)
    {
        MerchantId = merchantId;
        CorrelationId = correlationId;
    }

    public string MerchantId { get; }
    public string CorrelationId { get; }
}

public sealed class PaymentsContextFactory : IContextFactory<PaymentsContext>
{
    private readonly SampleClock _clock;

    public PaymentsContextFactory(SampleClock clock)
    {
        _clock = clock;
    }

    public ValueTask<PaymentsContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(new PaymentsContext("merchant-42", "payments-" + _clock.UtcNow));
    }
}

public sealed class CapturePaymentHandler : ICommandHandler<CapturePayment, PaymentsContext>
{
    public Task HandleAsync(CapturePayment command, PaymentsContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"handler payments capture {command.PaymentId} amount={command.Amount} merchant={context.MerchantId}");
        return Task.CompletedTask;
    }
}

public sealed class RefundPaymentHandler : ICommandHandler<RefundPayment, PaymentsContext>
{
    public Task HandleAsync(RefundPayment command, PaymentsContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"handler payments refund {command.PaymentId} merchant={context.MerchantId}");
        return Task.CompletedTask;
    }
}

[TinyPolicy]
[UseMiddleware(typeof(PaymentPolicyMiddleware<,>))]
[ForCommand(typeof(CapturePayment))]
[ForCommand(typeof(RefundPayment))]
public sealed class PaymentRiskPolicy;

public sealed class PaymentAuditMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"per-command payment audit {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}

public sealed class PaymentPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"policy payment risk {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}
