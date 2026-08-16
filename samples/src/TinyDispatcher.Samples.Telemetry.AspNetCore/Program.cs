using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TinyDispatcher;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Samples.Telemetry.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.UseTinyDispatcher<TinyDispatcher.AppContext>(_ => { });
builder.Services.AddHostedService<OrderReconciliationWorker>();

builder.Services
    .AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(
        serviceName: "TinyShop.AspNetCore",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddSource(WorkerTelemetry.ActivitySourceName)
        .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter(TinyDispatcherTelemetry.MeterName)
        .AddConsoleExporter());

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    sample = "TinyDispatcher OpenTelemetry ASP.NET Core",
    worker = "Runs one reconciliation cycle after startup"
}));

app.MapPost("/orders", async (
    CreateOrderRequest request,
    IDispatcher<TinyDispatcher.AppContext> dispatcher,
    CancellationToken cancellationToken) =>
{
    await dispatcher.DispatchAsync(
        new CreateOrderCommand(request.OrderId, request.CustomerEmail),
        cancellationToken);

    return Results.Accepted($"/orders/{request.OrderId}");
});

app.MapGet("/orders/{orderId}", async (
    string orderId,
    IDispatcher<TinyDispatcher.AppContext> dispatcher,
    CancellationToken cancellationToken) =>
{
    var status = await dispatcher.DispatchAsync<GetOrderStatusQuery, string>(
        new GetOrderStatusQuery(orderId),
        cancellationToken);

    return Results.Ok(new { orderId, status });
});

app.MapPost("/payments/fail", async (
    IDispatcher<TinyDispatcher.AppContext> dispatcher,
    CancellationToken cancellationToken) =>
{
    try
    {
        await dispatcher.DispatchAsync(new FailPaymentCommand(), cancellationToken);
        return Results.NoContent();
    }
    catch (PaymentDeclinedException exception)
    {
        return Results.Problem(exception.Message, statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapPost("/orders/cancel", async (
    IDispatcher<TinyDispatcher.AppContext> dispatcher) =>
{
    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();

    try
    {
        await dispatcher.DispatchAsync(new CancelOrderCommand(), cancellation.Token);
        return Results.NoContent();
    }
    catch (OperationCanceledException)
    {
        return Results.Ok(new { outcome = "canceled" });
    }
});

app.Run("http://localhost:5099");

public sealed record CreateOrderRequest(string OrderId, string CustomerEmail);
