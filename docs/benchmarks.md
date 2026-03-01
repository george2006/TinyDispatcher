# Benchmarks

This document indexes all performance benchmarks for TinyDispatcher.

## Methodology

All benchmarks:

- Use BenchmarkDotNet
- Run in Release mode
- Use scoped DI
- Are executed multiple times
- Include full raw output for transparency

---

## Available Benchmarks

| Scenario | Description | Result Summary |
|----------|------------|---------------|
| [Zero Middleware (10 runs)](benchmarks/zero_middleware.md) | Baseline dispatch cost | ~9.5% faster, -40B alloc |
| [Middleware x10 (9 runs)](benchmarks/ten_middleware.md) | Heavy pipeline (10 middleware layers) | ~12–15% faster avg, -1192B alloc |