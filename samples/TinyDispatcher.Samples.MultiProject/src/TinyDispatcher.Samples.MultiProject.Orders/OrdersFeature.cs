using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyAppContext = TinyDispatcher.AppContext;

namespace TinyDispatcher.Samples.MultiProject.Orders;

public sealed record CreateOrder(string OrderId, decimal Amount) : ICommand;

public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, TinyAppContext>
{
    public Task HandleAsync(CreateOrder command, AppContext context, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[Orders.Handler] Created order {command.OrderId} for {command.Amount:0.00}");

        return Task.CompletedTask;
    }
}
