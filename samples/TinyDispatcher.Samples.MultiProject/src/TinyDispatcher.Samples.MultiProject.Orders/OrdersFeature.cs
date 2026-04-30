using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Pipeline;
using TinyAppContext = TinyDispatcher.AppContext;

[assembly: TinyDispatcherPipelineContribution(new[]
{
    typeof(TinyDispatcher.Samples.MultiProject.Orders.OrdersActivityMiddleware<,>)
})]
[assembly: TinyDispatcherPolicyContribution(
    typeof(TinyDispatcher.Samples.MultiProject.Orders.OrdersPolicy),
    new[] { typeof(TinyDispatcher.Samples.MultiProject.Orders.OrdersPolicyMiddleware<,>) },
    new[] { typeof(TinyDispatcher.Samples.MultiProject.Orders.CreateOrder) })]

namespace TinyDispatcher.Samples.MultiProject.Orders;

public sealed record CreateOrder(string OrderId, decimal Amount) : ICommand;

[TinyPolicy]
[UseMiddleware(typeof(OrdersPolicyMiddleware<,>))]
[ForCommand(typeof(CreateOrder))]
public sealed class OrdersPolicy { }

public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, TinyAppContext>
{
    public Task HandleAsync(CreateOrder command, AppContext context, CancellationToken ct = default)
    {
        Console.WriteLine(
            $"[Orders.Handler] Created order {command.OrderId} for {command.Amount:0.00}");

        return Task.CompletedTask;
    }
}

public sealed class OrdersActivityMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Orders.Global] -> {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        Console.WriteLine($"[Orders.Global] <- {typeof(TCommand).Name}");
    }
}

public sealed class OrdersPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"[Orders.Policy] checking {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}
