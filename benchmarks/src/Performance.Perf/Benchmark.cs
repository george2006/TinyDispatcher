using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Microsoft.Extensions.DependencyInjection;
using Performance.Mediatr;
using Performance.Tiny;
using System.Threading.Tasks;

namespace Performance.Perf;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 20)]
public class DispatcherBenchmarks
{
    private PingCommand _cmd = default!;
    private MediatRFixture.PingRequest _request = default!;

    private MediatRFixture _mediatrFixture = default!;
    private TinyDispatcherFixture _tinyFixture = default!;

    // per-iteration scope + runners (correct scoped behavior, low overhead)
    private IServiceScope _mediatrScope = default!;
    private IServiceScope _tinyScope = default!;
    private MediatRFixture.ScopeRunner _mediatr = default!;
    private TinyDispatcherFixture.ScopeRunner _tiny = default!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _cmd = new PingCommand();
        _request = new MediatRFixture.PingRequest();

        _mediatrFixture = new MediatRFixture();
        _mediatrFixture.Build();

        _tinyFixture = new TinyDispatcherFixture();
        _tinyFixture.Build();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _mediatrFixture.Cleanup();
        _tinyFixture.Cleanup();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _mediatrScope = _mediatrFixture.ServiceProvider.CreateScope();
        _tinyScope = _tinyFixture.ServiceProvider.CreateScope();

        _mediatr = _mediatrFixture.CreateRunner(_mediatrScope.ServiceProvider);
        _tiny = _tinyFixture.CreateRunner(_tinyScope.ServiceProvider);

        // warm-up inside same scope to avoid first-call artifacts
        _mediatr.Send(_request).GetAwaiter().GetResult();
        _tiny.Dispatch(_cmd).GetAwaiter().GetResult();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _mediatrScope.Dispose();
        _tinyScope.Dispose();
    }

    [Benchmark(Baseline = true)]
    public Task MediatR_Send() => _mediatr.Send(_request);

    [Benchmark]
    public Task Tiny_Dispatch() => _tiny.Dispatch(_cmd);
}