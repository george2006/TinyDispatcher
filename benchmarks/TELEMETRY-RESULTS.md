# TinyDispatcher Telemetry Benchmark

## Purpose

Measure successful command dispatch with the current TinyDispatcher project under four native .NET telemetry listener configurations.

This benchmark isolates instrumentation-listener cost. It does not include exporter, network, collector, or backend cost.

## Reproduce

```powershell
dotnet run --project benchmarks/src/Telemetry.Perf/Telemetry.Perf.csproj -c Release -- --filter "*" --job Short
```

## Environment

```text
Date: 2026-08-16
BenchmarkDotNet: 0.15.8
OS: Windows 11 10.0.26200.9168
CPU: Intel Core Ultra 7 165H, 16 physical / 22 logical cores
.NET SDK: 10.0.201
Runtime: .NET 10.0.5, X64 RyuJIT
GC: Concurrent Workstation
Job: ShortRun, 3 warmups, 3 measured iterations
```

## Result

| Listeners | Mean | Allocated |
| --- | ---: | ---: |
| None | 95.50 ns | 24 B |
| Activity | 361.26 ns | 680 B |
| Meter | 89.18 ns | 24 B |
| Activity and Meter | 368.52 ns | 680 B |

The small timing difference between `None` and `Meter` is measurement noise in this short run; it is not evidence that enabling metrics makes dispatch faster. The useful observations are:

- the no-listener dispatch remains sub-100 ns in this focused setup
- enabling the Meter listener adds no observed managed allocation
- a recorded Activity accounts for the observed listener allocation increase
- adding Meter collection beside Activity collection does not add observed managed allocation

These results justify retaining runtime-owned instrumentation. They do not justify a broad product performance claim. Re-run the benchmark on release candidates and representative deployment hardware before publishing performance claims.
