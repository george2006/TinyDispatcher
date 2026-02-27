# TinyDispatcher vs MediatR – Benchmark Results

This document presents benchmark results comparing **TinyDispatcher** and **MediatR**.

The focus is strictly on the **dispatch hot path**:

- Send / Dispatch call
- Middleware / Behavior pipeline execution
- Handler execution

No startup, DI build time, logging output, or I/O is measured.

---

# Environment

BenchmarkDotNet v0.15.8  
Windows 11 (10.0.26200.7922)  
Intel Core Ultra 7 165H 3.80GHz (22 logical / 16 physical cores)  
.NET SDK 10.0.103  
Runtime: .NET 10.0.3, x64 RyuJIT  

Job configuration:
- IterationCount = 20
- WarmupCount = 5
- LaunchCount = 1
- RunStrategy = Throughput
- Release build only

---

# Fair Comparison Rules

Both frameworks were benchmarked under identical constraints:

- Same DI container: Microsoft.Extensions.DependencyInjection
- Same middleware/behavior logic:
  - Pre: BlackHole.Consume(2)
  - Await next
  - Post: BlackHole.Consume(3)
- Same handler logic: BlackHole.Consume(1)
- Behaviors registered as Transient in MediatR
- Tiny middlewares registered as Transient via generated TryAddTransient
- MediatR handler returns Unit.Task (no per-call Task allocation)
- Tiny handler returns Task.CompletedTask
- Setup occurs in GlobalSetup (not measured)

---

# Results

## 0 Middleware (Pure Dispatch)

| Method | Mean | Allocated |
|--------|------|-----------|
| MediatR_Send | 41.00 ns | 128 B |
| Tiny_Dispatch | 37.89 ns | 152 B |

Summary:
- ~8% faster
- Slightly higher allocations for Tiny (pipeline/context overhead)
- Near parity, as expected in minimal scenario

---

## 1 Middleware

| Method | Mean | Allocated |
|--------|------|-----------|
| MediatR_Send | 89.41 ns | 368 B |
| Tiny_Dispatch | 57.25 ns | 152 B |

Summary:
- ~36% faster
- ~2.4× fewer allocations

---

## 2 Middleware

| Method | Mean | Allocated |
|--------|------|-----------|
| MediatR_Send | 138.46 ns | 512 B |
| Tiny_Dispatch | 76.26 ns | 152 B |

Summary:
- ~43% faster
- ~3.3× fewer allocations

---

## 5 Middleware

| Method | Mean | Allocated |
|--------|------|-----------|
| MediatR_Send | 194.1 ns | 944 B |
| Tiny_Dispatch | 158.4 ns | 152 B |

Summary:
- ~18% faster
- ~6× fewer allocations

---

## 10 Middleware

| Method | Mean | Allocated |
|--------|------|-----------|
| MediatR_Send | 528.2 ns | 1664 B |
| Tiny_Dispatch | 423.4 ns | 152 B |

Summary:
- ~20% faster
- ~9–11× fewer allocations

---

# Interpretation

### Pure dispatch (0 middleware)
Both frameworks are close in performance. This is expected.

### Short pipelines (1–2 middleware)
TinyDispatcher shows significant gains because fixed runtime overhead in delegate-based pipelines becomes visible.

### Longer pipelines (5–10 middleware)
Relative speed gains stabilize (~15–20%), but allocation savings remain substantial.

This reflects the architectural difference:

TinyDispatcher:
- Compile-time generated pipeline
- Direct invocation chain (switch-based execution)
- No runtime delegate composition

MediatR:
- Runtime pipeline composition
- Delegate-based behavior chaining

---

# Hot Path Summary

The measured hot path includes:

1. Resolving pipeline
2. Executing middleware/behavior chain
3. Executing handler
4. Returning Task/ValueTask

It does NOT include:

- Container construction
- Logging I/O
- External services
- Real business logic

---

# Reproducing the Benchmarks

From repository root:

dotnet run -c Release --project benchmarks/src/Performance.Perf

Middleware scenarios are selected via compile-time symbols:

- MW0
- MW1
- MW2
- MW5
- MW10

---

# Future Benchmarks

Next step:
- Compare TinyDispatcher.AppContext vs NoOpContext
- Measure impact of context size independently

# Feedback & Validation

If you believe there is any mistake, unfair setup, or measurement issue
in these benchmarks, please open an issue in the repository.

Constructive feedback and corrections are welcome.
