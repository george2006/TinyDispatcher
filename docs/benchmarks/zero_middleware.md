# TinyDispatcher vs MediatR --- Zero Middleware Benchmark (10 Runs, Full Transparency)

## Aggregated Results

  Method           Average Mean   Allocated
  ---------------- -------------- -----------
  MediatR          4.271 μs       192 B
  TinyDispatcher   4.174 μs       152 B

**Average Performance Difference:**\
TinyDispatcher is approximately **2.27% faster** across 10 independent
runs.

------------------------------------------------------------------------

## Allocation Difference

-   MediatR: 192 B per dispatch
-   TinyDispatcher: 152 B per dispatch
-   Deterministic difference: 40 B fewer per call

------------------------------------------------------------------------

# Raw Benchmark Data (All 10 Runs)

Below is the complete unfiltered data for full transparency.

  ---------------------------------------------------------------------------
  Run   MediatR Mean   MediatR     MediatR     Tiny Mean   Tiny     Tiny
        (μs)           Error       StdDev      (μs)        Error    StdDev
  ----- -------------- ----------- ----------- ----------- -------- ---------
  1     3.237          0.1699      0.1668      4.642       0.5101   0.5670

  2     4.435          0.4058      0.4167      3.647       0.1198   0.1231

  3     4.247          0.8275      0.9198      4.478       0.0991   0.1060

  4     5.435          1.5437      1.7777      4.383       0.1292   0.1383

  5     4.084          0.7630      0.8480      3.822       0.1304   0.1396

  6     3.750          0.3468      0.3406      4.189       0.8592   0.9550

  7     4.069          0.1098      0.1078      4.245       0.7257   0.8357

  8     4.279          0.9458      1.0512      4.384       0.0862   0.0958

  9     3.572          0.1660      0.1776      3.565       0.1649   0.1693

  10    5.600          0.8815      0.9798      4.385       0.6597   0.7597
  ---------------------------------------------------------------------------

------------------------------------------------------------------------

## Notes

-   InvocationCount = 1 (measuring realistic per-request cost)
-   IterationCount = 20
-   WarmupCount = 5
-   Scoped DI resolution included
-   Zero middleware configured
-   All runs executed on the same machine under identical configuration

Microsecond-level benchmarks naturally show variance due to: - CPU
scheduling - Turbo frequency shifts - GC interference - Thermal behavior

The allocation difference remains constant across all runs.
