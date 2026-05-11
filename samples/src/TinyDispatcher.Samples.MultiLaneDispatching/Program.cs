using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Samples.MultiLaneDispatching.DefaultAppContext;
using TinyDispatcher.Samples.MultiLaneDispatching.NoOp;
using TinyDispatcher.Samples.MultiLaneDispatching.Orders;
using TinyDispatcher.Samples.MultiLaneDispatching.Payments;
using TinyDispatcher.Samples.MultiLaneDispatching.Shared;
using TinyAppContext = TinyDispatcher.AppContext;

namespace TinyDispatcher.Samples.MultiLaneDispatching;

internal static class Program
{
    public static async Task Main()
    {
        var services = new ServiceCollection();

        services.AddSingleton(new SampleClock("2026-05-10T18:30:00Z"));
        services.AddOrdersLane();
        services.AddPaymentsLane();
        services.AddDefaultAppContextLane();
        services.AddNoOpLane();

        using var provider = services.BuildServiceProvider();

        Console.WriteLine("=== TinyDispatcher multi-lane sample ===");
        Console.WriteLine();

        await DispatchOrders(provider);
        await DispatchPayments(provider);
        await DispatchDefaultAppContext(provider);
        await DispatchNoOp(provider);

        Console.WriteLine();
        Console.WriteLine("Sample complete.");
    }

    private static async Task DispatchOrders(ServiceProvider provider)
    {
        Console.WriteLine("-- OrdersContext");
        var dispatcher = provider.GetRequiredService<IDispatcher<OrdersContext>>();

        await dispatcher.DispatchAsync(new SubmitOrder("ORD-1001"));
        await dispatcher.DispatchAsync(new CancelOrder("ORD-1001"));

        Console.WriteLine();
    }

    private static async Task DispatchPayments(ServiceProvider provider)
    {
        Console.WriteLine("-- PaymentsContext");
        var dispatcher = provider.GetRequiredService<IDispatcher<PaymentsContext>>();

        await dispatcher.DispatchAsync(new CapturePayment("PAY-2001", 42.50m));
        await dispatcher.DispatchAsync(new RefundPayment("PAY-2001"));

        Console.WriteLine();
    }

    private static async Task DispatchDefaultAppContext(ServiceProvider provider)
    {
        Console.WriteLine("-- AppContext default factory");
        var dispatcher = provider.GetRequiredService<IDispatcher<TinyAppContext>>();

        await dispatcher.DispatchAsync(new ShowFeature("default-factory"));

        Console.WriteLine();
    }

    private static async Task DispatchNoOp(ServiceProvider provider)
    {
        Console.WriteLine("-- NoOpContext");
        var dispatcher = provider.GetRequiredService<IDispatcher<NoOpContext>>();

        await dispatcher.DispatchAsync(new PingNoOp("fast-lane"));
    }
}
