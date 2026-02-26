using BenchmarkDotNet.Attributes;
using Performance.Mediatr;
using Performance.Shared;
using Performance.Tiny;
using BenchmarkDotNet.Engines;
using static Performance.Mediatr.MediatRFixture;


namespace Performance.Perf;

[MemoryDiagnoser]
[ThreadingDiagnoser]
[SimpleJob(RunStrategy.Throughput, launchCount: 1, warmupCount: 5, iterationCount: 20)]
public class DispatcherBenchmarks
{
    private PingCommand _cmd = default!;
    private PingRequest _request = default!;
    private MediatRFixture _mediatr = default!;
    private TinyDispatcherFixture _tiny = default!;
 
    [GlobalSetup]
    public void Setup()
    {
        _cmd = new PingCommand();
        _request = new PingRequest();
      
        _mediatr = new MediatRFixture();
        _mediatr.Build();

        _tiny = new TinyDispatcherFixture();
        _tiny.Build();

        // Warm-up to remove "first run" noise (JIT / DI caches)
        _mediatr.Send(_request).GetAwaiter().GetResult();
        _tiny.Dispatch(_cmd).GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public Task MediatR_Send() => _mediatr.Send(_request);

    [Benchmark]
    public Task Tiny_Dispatch() => _tiny.Dispatch(_cmd);
}
