using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TinyDispatcher;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Samples.Telemetry.Console;

const string sampleActivitySourceName = "TinyDispatcher.Samples.Telemetry.Console";

var resource = ResourceBuilder.CreateDefault().AddService(
    serviceName: "TinyShop.Console",
    serviceVersion: "1.0.0");

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(resource)
    .AddSource(sampleActivitySourceName)
    .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
    .AddConsoleExporter()
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(resource)
    .AddMeter(TinyDispatcherTelemetry.MeterName)
    .AddConsoleExporter()
    .Build();

var services = new ServiceCollection();
services.UseTinyDispatcher<TinyDispatcher.AppContext>(_ => { });

await using var provider = services.BuildServiceProvider();
var dispatcher = provider.GetRequiredService<IDispatcher<TinyDispatcher.AppContext>>();

using var sampleActivitySource = new ActivitySource(sampleActivitySourceName);
using (sampleActivitySource.StartActivity("checkout", ActivityKind.Internal))
{
    System.Console.WriteLine("Dispatching CreateOrderCommand with a nested ReserveInventoryCommand...");
    await dispatcher.DispatchAsync(new CreateOrderCommand(
        OrderId: "ORD-48291",
        CustomerEmail: "private@example.com"));
}

System.Console.WriteLine("Dispatching GetOrderStatusQuery...");
var status = await dispatcher.DispatchAsync<GetOrderStatusQuery, string>(
    new GetOrderStatusQuery("ORD-48291"));
System.Console.WriteLine($"Query result: {status}");

System.Console.WriteLine("Dispatching FailPaymentCommand...");
try
{
    await dispatcher.DispatchAsync(new FailPaymentCommand());
}
catch (PaymentDeclinedException)
{
    System.Console.WriteLine("Payment failure was preserved for the caller.");
}

System.Console.WriteLine("Dispatching CancelOrderCommand...");
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();

    try
    {
        await dispatcher.DispatchAsync(new CancelOrderCommand(), cancellation.Token);
    }
    catch (OperationCanceledException)
    {
        System.Console.WriteLine("Cooperative cancellation was preserved for the caller.");
    }
}

meterProvider.Dispose();
tracerProvider.Dispose();

System.Console.WriteLine("Telemetry export complete.");
