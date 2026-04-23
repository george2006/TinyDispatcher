using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyAppContext = TinyDispatcher.AppContext;

namespace TinyDispatcher.Samples.MultiProject.Billing;

public sealed record CapturePayment(string PaymentId, decimal Amount) : ICommand;

public sealed class CapturePaymentHandler : ICommandHandler<CapturePayment, TinyAppContext>
{
    public Task HandleAsync(CapturePayment command, AppContext context, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[Billing.Handler] Captured payment {command.PaymentId} for {command.Amount:0.00}");

        return Task.CompletedTask;
    }
}
