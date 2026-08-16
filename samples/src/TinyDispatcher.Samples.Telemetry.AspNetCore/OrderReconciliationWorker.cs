using System.Diagnostics;
using TinyDispatcher.Dispatching;

namespace TinyDispatcher.Samples.Telemetry.AspNetCore;

public static class WorkerTelemetry
{
    public const string ActivitySourceName = "TinyDispatcher.Samples.Telemetry.AspNetCore.Worker";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}

public sealed class OrderReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderReconciliationWorker> _logger;

    public OrderReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        _logger.LogInformation("Starting the one-shot order reconciliation sample.");

        using var activity = WorkerTelemetry.ActivitySource.StartActivity(
            "order-reconciliation",
            ActivityKind.Internal);
        using var scope = _scopeFactory.CreateScope();
        var dispatcher = scope.ServiceProvider
            .GetRequiredService<IDispatcher<TinyDispatcher.AppContext>>();

        await dispatcher.DispatchAsync(new ReconcileOrdersCommand(), stoppingToken);

        _logger.LogInformation("Order reconciliation sample completed.");
    }
}
