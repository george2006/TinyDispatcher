using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;
using TinyDispatcher.Samples.MultiContextRc.Shared;

namespace TinyDispatcher.Samples.MultiContextRc.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersLane(this IServiceCollection services)
    {
        services.AddTransient(typeof(ConsoleLogMiddleware<,>));
        services.AddTransient(typeof(OrderValidationMiddleware<,>));
        services.AddTransient(typeof(OrderPolicyMiddleware<,>));

        services.UseTinyDispatcher<OrdersContext>(tiny =>
        {
            tiny.UseFactory<OrdersContextFactory>();
            tiny.UseGlobalMiddleware(typeof(ConsoleLogMiddleware<,>));
            tiny.UseMiddlewareFor<SubmitOrder>(typeof(OrderValidationMiddleware<,>));
            tiny.UsePolicy<OrderApprovalPolicy>();
        });

        return services;
    }
}

public sealed record SubmitOrder(string OrderId) : ICommand;
public sealed record CancelOrder(string OrderId) : ICommand;

public sealed class OrdersContext
{
    public OrdersContext(string tenant, string correlationId)
    {
        Tenant = tenant;
        CorrelationId = correlationId;
    }

    public string Tenant { get; }
    public string CorrelationId { get; }
}

public sealed class OrdersContextFactory : IContextFactory<OrdersContext>
{
    private readonly SampleClock _clock;

    public OrdersContextFactory(SampleClock clock)
    {
        _clock = clock;
    }

    public ValueTask<OrdersContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(new OrdersContext("orders-eu", "orders-" + _clock.UtcNow));
    }
}

public sealed class SubmitOrderHandler : ICommandHandler<SubmitOrder, OrdersContext>
{
    public Task HandleAsync(SubmitOrder command, OrdersContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"handler orders submit {command.OrderId} tenant={context.Tenant}");
        return Task.CompletedTask;
    }
}

public sealed class CancelOrderHandler : ICommandHandler<CancelOrder, OrdersContext>
{
    public Task HandleAsync(CancelOrder command, OrdersContext context, CancellationToken ct = default)
    {
        Console.WriteLine($"handler orders cancel {command.OrderId} tenant={context.Tenant}");
        return Task.CompletedTask;
    }
}

[TinyPolicy]
[UseMiddleware(typeof(OrderPolicyMiddleware<,>))]
[ForCommand(typeof(SubmitOrder))]
[ForCommand(typeof(CancelOrder))]
public sealed class OrderApprovalPolicy;

public sealed class OrderValidationMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"per-command order validation {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}

public sealed class OrderPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        Console.WriteLine($"policy order approval {typeof(TCommand).Name}");
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
    }
}
