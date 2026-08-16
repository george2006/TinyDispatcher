using System.Diagnostics;
using System.Diagnostics.Metrics;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;

namespace Telemetry.Perf;

[MemoryDiagnoser]
public class DispatcherTelemetryBenchmarks
{
    private ServiceProvider _services = default!;
    private IDispatcher<BenchmarkContext> _dispatcher = default!;
    private ActivityListener? _activityListener;
    private MeterListener? _meterListener;

    [Params(
        ListenerConfiguration.None,
        ListenerConfiguration.Activity,
        ListenerConfiguration.Meter,
        ListenerConfiguration.ActivityAndMeter)]
    public ListenerConfiguration Listeners { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ConfigureListeners();

        var services = new ServiceCollection();
        services.AddSingleton<IContextFactory<BenchmarkContext>, BenchmarkContextFactory>();
        services.AddSingleton<ICommandHandler<BenchmarkCommand, BenchmarkContext>, BenchmarkCommandHandler>();
        services.AddSingleton<IDispatcher<BenchmarkContext>>(provider =>
            new Dispatcher<BenchmarkContext>(
                provider,
                provider.GetRequiredService<IContextFactory<BenchmarkContext>>()));

        _services = services.BuildServiceProvider();
        _dispatcher = _services.GetRequiredService<IDispatcher<BenchmarkContext>>();
    }

    [Benchmark]
    public Task Dispatch()
    {
        return _dispatcher.DispatchAsync(new BenchmarkCommand());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _activityListener?.Dispose();
        _meterListener?.Dispose();
        _services.Dispose();
    }

    private void ConfigureListeners()
    {
        if (Listeners is ListenerConfiguration.Activity or ListenerConfiguration.ActivityAndMeter)
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == TinyDispatcherTelemetry.ActivitySourceName,
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        if (Listeners is ListenerConfiguration.Meter or ListenerConfiguration.ActivityAndMeter)
        {
            _meterListener = new MeterListener
            {
                InstrumentPublished = static (instrument, listener) =>
                {
                    if (instrument.Meter.Name == TinyDispatcherTelemetry.MeterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                }
            };
            _meterListener.SetMeasurementEventCallback<long>(static (_, _, _, _) => { });
            _meterListener.SetMeasurementEventCallback<double>(static (_, _, _, _) => { });
            _meterListener.Start();
        }
    }

    public enum ListenerConfiguration
    {
        None,
        Activity,
        Meter,
        ActivityAndMeter
    }

    private sealed record BenchmarkCommand : ICommand;

    private sealed class BenchmarkCommandHandler : ICommandHandler<BenchmarkCommand, BenchmarkContext>
    {
        public Task HandleAsync(
            BenchmarkCommand command,
            BenchmarkContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class BenchmarkContext;

    private sealed class BenchmarkContextFactory : IContextFactory<BenchmarkContext>
    {
        private static readonly BenchmarkContext Context = new();

        public ValueTask<BenchmarkContext> CreateAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Context);
        }
    }
}
