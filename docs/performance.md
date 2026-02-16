# Performance notes

TinyDispatcher is designed to keep the runtime hot path small and predictable.

Key ideas:

- handler discovery happens at build time (no runtime scanning)
- middleware composition is emitted as code (no delegate chains)
- pipelines execute via a switch-based runner
- the generated code is intended to be readable and debuggable

## Practical guidance

- Benchmark in Release mode.
- Treat middleware as normal DI services; keep global middleware minimal and cheap.
- Prefer deterministic layering over “smart” dynamic composition.
