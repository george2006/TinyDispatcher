using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Pipeline;
using TinyAppContext = TinyDispatcher.AppContext;

[assembly: TinyDispatcherPipelineContribution(new[]
{
    typeof(TinyDispatcher.Samples.MultiProject.Billing.BillingActivityMiddleware<,>)
})]
[assembly: TinyDispatcherPolicyContribution(
    typeof(TinyDispatcher.Samples.MultiProject.Billing.BillingPolicy),
    new[] { typeof(TinyDispatcher.Samples.MultiProject.Billing.BillingPolicyMiddleware<,>) },
    new[] { typeof(TinyDispatcher.Samples.MultiProject.Billing.CapturePayment) })]

namespace TinyDispatcher.Samples.MultiProject.Billing;

public sealed record CapturePayment(string PaymentId, decimal Amount) : ICommand;

[TinyPolicy]
[UseMiddleware(typeof(BillingPolicyMiddleware<,>))]
[ForCommand(typeof(CapturePayment))]
public sealed class BillingPolicy { }

public sealed class CapturePaymentHandler : ICommandHandler<CapturePayment, TinyAppContext>
{
    public Task HandleAsync(CapturePayment command, AppContext context, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[Billing.Handler] Captured payment {command.PaymentId} for {command.Amount:0.00}");

        return Task.CompletedTask;
    }
}

public sealed class BillingActivityMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Billing.Global] -> {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        Console.WriteLine($"[Billing.Global] <- {typeof(TCommand).Name}");
    }
}

public sealed class BillingPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Billing.Policy] checking {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}
