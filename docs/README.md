# Documentation

If you only read one thing first, start with **Getting Started** and then skim **Architecture**.

Key capabilities (high level):

- compile-time discovery and validation (source generator)
- generated, deterministic middleware pipelines
- explicit `TContext` with **pluggable context factories**
- optional **no-op context** mode (`UseTinyNoOpContext`) when you don't need context
- optional feature composition via `AppContext` + `IFeatureInitializer`

- [Getting Started](getting-started.md)
- [Architecture](architecture.md)
- [Source Generator](source-generator.md)
- [Middleware](middleware.md)
- [Pipelines & Layering](pipelines.md)
- [Context & Features](context.md)
- [Pipeline Maps](pipeline-maps.md)
- [Performance Notes](performance.md)
- [Migration Guide](migration.md)
- [Design Decisions](design-decisions.md)
- [Benchmarks](docs/benchmarks.md)

