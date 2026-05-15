#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using Xunit;

namespace TinyDispatcher.UnitTests;

public sealed class MultiLaneDispatchingTests
{
    [Fact]
    public async Task Executes_only_the_middleware_configured_for_the_selected_lane()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MultiLaneExecutionRecorder>();
        TinyDispatcher.Generated.ThisAssemblyContribution.AddServices(services);

        services.UseTinyDispatcher<OrdersLaneContext>(tiny =>
        {
            tiny.UseContextFactory<OrdersLaneContextFactory>();
            tiny.UseGlobalMiddleware(typeof(OrdersGlobalMiddleware<,>));
            tiny.UseMiddlewareFor<SubmitOrder>(typeof(OrdersCommandMiddleware<,>));
            tiny.UsePolicy<OrdersPolicy>();
        });

        services.UseTinyDispatcher<PaymentsLaneContext>(tiny =>
        {
            tiny.UseContextFactory<PaymentsLaneContextFactory>();
            tiny.UseGlobalMiddleware(typeof(PaymentsGlobalMiddleware<,>));
            tiny.UseMiddlewareFor<CapturePayment>(typeof(PaymentsCommandMiddleware<,>));
            tiny.UsePolicy<PaymentsPolicy>();
        });

        using var provider = services.BuildServiceProvider();

        var orders = provider.GetRequiredService<IDispatcher<OrdersLaneContext>>();
        var payments = provider.GetRequiredService<IDispatcher<PaymentsLaneContext>>();

        await orders.DispatchAsync(new SubmitOrder("ORD-1001"));
        await payments.DispatchAsync(new CapturePayment("PAY-2001"));

        var recorder = provider.GetRequiredService<MultiLaneExecutionRecorder>();

        Assert.Equal(
            new[]
            {
                "orders:global:before",
                "orders:policy:before",
                "orders:command:before",
                "orders:handler",
                "orders:command:after",
                "orders:policy:after",
                "orders:global:after",
                "payments:global:before",
                "payments:policy:before",
                "payments:command:before",
                "payments:handler",
                "payments:command:after",
                "payments:policy:after",
                "payments:global:after"
            },
            recorder.Events);
    }
}

internal sealed record SubmitOrder(string OrderId) : ICommand;

internal sealed record CapturePayment(string PaymentId) : ICommand;

internal sealed class OrdersLaneContext
{
    public OrdersLaneContext(MultiLaneExecutionRecorder recorder)
    {
        Recorder = recorder;
    }

    public MultiLaneExecutionRecorder Recorder { get; }
}

internal sealed class PaymentsLaneContext
{
    public PaymentsLaneContext(MultiLaneExecutionRecorder recorder)
    {
        Recorder = recorder;
    }

    public MultiLaneExecutionRecorder Recorder { get; }
}

internal sealed class OrdersLaneContextFactory : IContextFactory<OrdersLaneContext>
{
    private readonly MultiLaneExecutionRecorder _recorder;

    public OrdersLaneContextFactory(MultiLaneExecutionRecorder recorder)
    {
        _recorder = recorder;
    }

    public ValueTask<OrdersLaneContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(new OrdersLaneContext(_recorder));
    }
}

internal sealed class PaymentsLaneContextFactory : IContextFactory<PaymentsLaneContext>
{
    private readonly MultiLaneExecutionRecorder _recorder;

    public PaymentsLaneContextFactory(MultiLaneExecutionRecorder recorder)
    {
        _recorder = recorder;
    }

    public ValueTask<PaymentsLaneContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(new PaymentsLaneContext(_recorder));
    }
}

internal sealed class SubmitOrderHandler : ICommandHandler<SubmitOrder, OrdersLaneContext>
{
    public Task HandleAsync(SubmitOrder command, OrdersLaneContext context, CancellationToken ct = default)
    {
        context.Recorder.Record("orders:handler");
        return Task.CompletedTask;
    }
}

internal sealed class CapturePaymentHandler : ICommandHandler<CapturePayment, PaymentsLaneContext>
{
    public Task HandleAsync(CapturePayment command, PaymentsLaneContext context, CancellationToken ct = default)
    {
        context.Recorder.Record("payments:handler");
        return Task.CompletedTask;
    }
}

[TinyPolicy]
[UseMiddleware(typeof(OrdersPolicyMiddleware<,>))]
[ForCommand(typeof(SubmitOrder))]
internal sealed class OrdersPolicy;

[TinyPolicy]
[UseMiddleware(typeof(PaymentsPolicyMiddleware<,>))]
[ForCommand(typeof(CapturePayment))]
internal sealed class PaymentsPolicy;

internal sealed class OrdersGlobalMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((OrdersLaneContext)(object)context!).Recorder;

        recorder.Record("orders:global:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("orders:global:after");
    }
}

internal sealed class OrdersPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((OrdersLaneContext)(object)context!).Recorder;

        recorder.Record("orders:policy:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("orders:policy:after");
    }
}

internal sealed class OrdersCommandMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((OrdersLaneContext)(object)context!).Recorder;

        recorder.Record("orders:command:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("orders:command:after");
    }
}

internal sealed class PaymentsGlobalMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((PaymentsLaneContext)(object)context!).Recorder;

        recorder.Record("payments:global:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("payments:global:after");
    }
}

internal sealed class PaymentsPolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((PaymentsLaneContext)(object)context!).Recorder;

        recorder.Record("payments:policy:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("payments:policy:after");
    }
}

internal sealed class PaymentsCommandMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default)
    {
        var recorder = ((PaymentsLaneContext)(object)context!).Recorder;

        recorder.Record("payments:command:before");
        await runtime.NextAsync(command, context, ct);
        recorder.Record("payments:command:after");
    }
}

internal sealed class MultiLaneExecutionRecorder
{
    private readonly List<string> _events = new();

    public IReadOnlyList<string> Events => _events;

    public void Record(string value)
    {
        _events.Add(value);
    }
}
