# TinyDispatcher vs MediatR
## 10 Middleware Benchmark (MW10)

## Overview

This benchmark compares **TinyDispatcher** and **MediatR** with **10
middleware layers** configured (MW10).\
The intent is to observe how each pipeline scales when the middleware
chain becomes deeper.

**Important:** the data below includes **scoped DI** and a realistic
per-dispatch cost model (**InvocationCount = 1**).

------------------------------------------------------------------------

## Benchmark Environment

-   OS: Windows 11\
-   CPU: Intel Core Ultra 7 165H (22 logical / 16 physical cores)\
-   .NET SDK: 10.0.103\
-   Runtime: .NET 10.0.3, X64 RyuJIT x86-64-v3\
-   Tooling: BenchmarkDotNet 0.15.8\
-   IterationCount: 20\
-   WarmupCount: 5\
-   InvocationCount: 1\
-   RunStrategy: Throughput

Each iteration includes:

1.  DI scope creation\
2.  Dispatcher/Mediator resolution\
3.  10-middleware pipeline execution\
4.  Handler invocation + await completion

------------------------------------------------------------------------

## Aggregated Results (Average of Provided Runs)

**Runs included:** 9  
(If you run an additional sample later, we can append it and recompute the averages.)

| Method          | Average Mean | Allocated |
|-----------------|-------------|-----------|
| MediatR         | 7.751 μs    | 1384 B    |
| TinyDispatcher  | 6.490 μs    | 192 B     |

### Performance Difference (Average)

TinyDispatcher is approximately **16.27% faster** on average across the provided runs.

### Performance Difference (Average)

TinyDispatcher is approximately **16.27% faster** on average across the
provided runs.

------------------------------------------------------------------------

## Allocation Difference

In all runs, allocations were:

-   MediatR: **1384 B** per dispatch\
-   TinyDispatcher: **192 B** per dispatch

That is a deterministic reduction of:

**1192 B fewer allocations per dispatch** with TinyDispatcher in this
MW10 scenario.

> Note: MediatR and TinyDispatcher do not implement pipelines the same
> way internally, so allocation differences are expected. The goal here
> is to measure *observed* end-to-end overhead under the same benchmark
> harness.

------------------------------------------------------------------------

## Observations

-   With deep middleware chains, **allocation behavior becomes a
    first-class signal**.
-   MediatR's allocation footprint is significantly higher in this
    configuration.
-   TinyDispatcher's allocations remain low and stable across runs.
-   Microsecond-level measurements can still show variance due to OS
    scheduling, turbo frequency shifts, GC interference, and thermal
    behavior.

------------------------------------------------------------------------

# Raw Benchmark Data (All Runs)

Full unfiltered run data for transparency:

## Raw Benchmark Data (All Runs)

Full unfiltered run data for transparency:

| Run | MediatR Mean (μs) | MediatR Error | MediatR StdDev | Tiny Mean (μs) | Tiny Error | Tiny StdDev |
|-----|-------------------|---------------|----------------|----------------|------------|-------------|
| 1   | 9.135 | 2.0715 | 2.3855 | 6.318 | 0.2844 | 0.2921 |
| 2   | 7.839 | 0.4326 | 0.4629 | 6.031 | 0.1273 | 0.1250 |
| 3   | 7.500 | 1.1818 | 1.3136 | 6.206 | 0.3213 | 0.3438 |
| 4   | 7.212 | 0.2794 | 0.2870 | 6.076 | 0.1705 | 0.1751 |
| 5   | 6.969 | 0.1377 | 0.1352 | 7.088 | 0.5770 | 0.5925 |
| 6   | 6.994 | 0.6278 | 0.6447 | 6.538 | 0.2752 | 0.2826 |
| 7   | 9.150 | 1.2670 | 1.3550 | 7.842 | 1.3510 | 1.5020 |
| 8   | 6.795 | 1.1951 | 1.3763 | 5.712 | 0.3998 | 0.4106 |
| 9   | 8.165 | 0.2035 | 0.2090 | 6.595 | 0.8974 | 0.9975 |
------------------------------------------------------------------------


## Conclusion


With **10 middleware layers**, TinyDispatcher shows:

-   A **lower average dispatch time** across the provided runs
-   A **much lower allocation footprint** per dispatch

Next steps that make this even stronger:

-   Run 10+ samples and compute median/min/max
-   Repeat with MW0, MW1, MW2, MW5 to visualize scaling
-   Add an ASP.NET minimal API scenario to complement micro-benchmarks
