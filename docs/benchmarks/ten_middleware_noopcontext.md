# TinyDispatcher vs MediatR — 10 Middleware + NoOpContext Benchmark (10 Runs)

## Overview

This benchmark compares **TinyDispatcher** and **MediatR** with:

- **10 middleware layers** (MW10)
- **Scoped DI**
- **InvocationCount = 1** (measuring realistic per-dispatch cost)
- TinyDispatcher running with **NoOpContext** (a more performant execution mode when handlers do not require an application context)

The goal is to quantify how much overhead can be removed when your handlers do not need a context object.

---

## Benchmark Environment

- OS: Windows 11  
- CPU: Intel Core Ultra 7 165H (22 logical / 16 physical cores)  
- .NET SDK: 10.0.103  
- Runtime: .NET 10.0.3, X64 RyuJIT x86-64-v3  
- Tooling: BenchmarkDotNet 0.15.8  
- WarmupCount: 5  
- IterationCount: 20  
- InvocationCount: 1  
- RunStrategy: Throughput  

Each iteration includes:

1. DI scope creation  
2. Dispatcher/Mediator resolution  
3. 10-layer pipeline execution  
4. Handler invocation + await completion  

---

## Aggregated Results (Average of 10 Runs)

| Method | Average Mean | Allocated |
|--------|--------------|-----------|
| MediatR | 7.586 μs | 1384 B |
| TinyDispatcher (NoOpContext) | 6.140 μs | 64 B |

### Performance Difference (Average)

TinyDispatcher (NoOpContext) is approximately **19.06% faster** on average across 10 independent runs.

---

## Allocation Difference

- MediatR: **1384 B** per dispatch  
- TinyDispatcher (NoOpContext): **64 B** per dispatch  

That is a deterministic reduction of:

- **1320 B fewer allocations per dispatch**
- ~**95.38%** lower allocations per dispatch in this scenario

This is the main advantage of NoOpContext: when context is unnecessary, the dispatcher can avoid carrying context-related overhead through the pipeline.

---

## Notes on Interpretation

- Microsecond-level benchmarks can vary due to OS scheduling, turbo frequency shifts, GC interference, and thermal behavior.
- The important signal here is that allocations remain stable (**64 B**) for TinyDispatcher under a deep middleware chain.

---

## Raw Benchmark Data (All 10 Runs)

Full unfiltered run data for transparency:

| Run | MediatR Mean (μs) | MediatR Error | MediatR StdDev | Tiny Mean (μs) | Tiny Error | Tiny StdDev |
|-----|-------------------|---------------|----------------|----------------|------------|-------------|
| 1 | 6.606 | 0.2276 | 0.2235 | 5.815 | 0.3419 | 0.3937 |
| 2 | 6.824 | 0.0810 | 0.0831 | 5.372 | 0.1891 | 0.2024 |
| 3 | 7.294 | 0.9673 | 1.0350 | 5.960 | 0.9028 | 1.0400 |
| 4 | 6.682 | 0.4648 | 0.4773 | 5.812 | 0.2348 | 0.2306 |
| 5 | 7.334 | 0.1446 | 0.1608 | 6.979 | 0.6568 | 0.7300 |
| 6 | 7.642 | 1.0237 | 1.1379 | 5.500 | 0.2504 | 0.2679 |
| 7 | 7.189 | 0.9459 | 1.0514 | 6.017 | 0.2907 | 0.3111 |
| 8 | 7.711 | 0.1269 | 0.1410 | 6.272 | 0.2882 | 0.3083 |
| 9 | 8.794 | 0.9751 | 1.0430 | 6.660 | 1.1645 | 1.3410 |
| 10 | 9.784 | 2.1651 | 2.4065 | 7.011 | 0.2460 | 0.2632 |

---

## Conclusion

With **10 middleware layers**, running TinyDispatcher with **NoOpContext** (when handlers do not need a context object) yields:

- A **~19.06%** lower average dispatch time (in this benchmark configuration)
- **1320 B fewer allocations per dispatch** (~95.38% less)

This makes NoOpContext a compelling execution mode for performance-sensitive pipelines where context is not required.
