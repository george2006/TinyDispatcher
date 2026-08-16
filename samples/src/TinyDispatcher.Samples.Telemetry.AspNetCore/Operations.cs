using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples.Telemetry.AspNetCore;

public sealed record CreateOrderCommand(string OrderId, string CustomerEmail) : ICommand;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, TinyDispatcher.AppContext>
{
    private readonly IDispatcher<TinyDispatcher.AppContext> _dispatcher;

    public CreateOrderCommandHandler(IDispatcher<TinyDispatcher.AppContext> dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public Task HandleAsync(
        CreateOrderCommand command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
    {
        return _dispatcher.DispatchAsync(
            new ReserveInventoryCommand(command.OrderId),
            cancellationToken);
    }
}

public sealed record ReserveInventoryCommand(string OrderId) : ICommand;

public sealed class ReserveInventoryCommandHandler : ICommandHandler<ReserveInventoryCommand, TinyDispatcher.AppContext>
{
    public Task HandleAsync(
        ReserveInventoryCommand command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed record GetOrderStatusQuery(string OrderId) : IQuery<string>;

public sealed class GetOrderStatusQueryHandler : IQueryHandler<GetOrderStatusQuery, string>
{
    public Task<string> HandleAsync(
        GetOrderStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult("ready");
    }
}

public sealed record ReconcileOrdersCommand : ICommand;

public sealed class ReconcileOrdersCommandHandler : ICommandHandler<ReconcileOrdersCommand, TinyDispatcher.AppContext>
{
    private readonly IDispatcher<TinyDispatcher.AppContext> _dispatcher;

    public ReconcileOrdersCommandHandler(IDispatcher<TinyDispatcher.AppContext> dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public async Task HandleAsync(
        ReconcileOrdersCommand command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
    {
        await _dispatcher.DispatchAsync<GetPendingOrdersQuery, int>(
            new GetPendingOrdersQuery(),
            cancellationToken);
    }
}

public sealed record GetPendingOrdersQuery : IQuery<int>;

public sealed class GetPendingOrdersQueryHandler : IQueryHandler<GetPendingOrdersQuery, int>
{
    public Task<int> HandleAsync(
        GetPendingOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}

public sealed record FailPaymentCommand : ICommand;

public sealed class FailPaymentCommandHandler : ICommandHandler<FailPaymentCommand, TinyDispatcher.AppContext>
{
    public Task HandleAsync(
        FailPaymentCommand command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
    {
        throw new PaymentDeclinedException("The payment provider declined the operation.");
    }
}

public sealed record CancelOrderCommand : ICommand;

public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, TinyDispatcher.AppContext>
{
    public Task HandleAsync(
        CancelOrderCommand command,
        TinyDispatcher.AppContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public sealed class PaymentDeclinedException : Exception
{
    public PaymentDeclinedException(string message)
        : base(message)
    {
    }
}
