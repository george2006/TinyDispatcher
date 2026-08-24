# TinyDispatcher

TinyDispatcher is a small, compile-time oriented dispatcher for .NET.

It provides a predictable, explicit, and performant command/query dispatch core by moving:

- handler discovery to **build time**
- middleware pipeline composition to **generated code** (also build time)

...while keeping runtime execution simple and scope-friendly.

## What you get

- **Compile-time handler discovery** (no runtime scanning/reflection)
- **Generated pipelines** (global middleware -> policy middleware -> per-command middleware -> handler)
- **Deterministic ordering** with declaration order preserved inside each middleware layer
- **Explicit context (`TContext`)** for command handlers
- **Multi-assembly composition** with host-owned final pipeline generation
- **Pluggable context factory** (delegate factory or DI registration)
- **Feature-friendly `AppContext`** (optional `IFeatureInitializer`-based composition)
- **Source-generator diagnostics** for invalid shapes/config (fail fast, no guessing)
- **Context lanes** in `1.2.0` for module-owned contexts and typed dispatchers
- **Built-in OpenTelemetry** traces and metrics for commands and queries
- **Generated application structure** available without dispatching operations or scanning assemblies

## Install

Stable release:

```bash
dotnet add package TinyDispatcher
```

`1.2.1` is the current stable release. It includes context lanes and preserves middleware declaration order within each pipeline layer. `1.1.x` remains available for applications that are not ready to adopt the multi-context API.

## Quick start

Define a command:

```csharp
public sealed record CreateOrder(string OrderId) : ICommand;
```

Define a context-aware handler:

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
{
    public Task HandleAsync(CreateOrder command, AppContext ctx, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

Register:

```csharp
services.UseTinyDispatcher<AppContext>(tiny =>
{
    // optional: middleware, policies, features
});
```

For a custom context, register its factory in DI before bootstrapping:

```csharp
services.AddScoped<IContextFactory<MyContext>, MyContextFactory>();

services.UseTinyDispatcher<MyContext>(tiny =>
{
    // optional: middleware, policies, features
});
```

Dispatch:

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
```

## Multi-assembly composition

TinyDispatcher supports a modular setup where:

- handlers can live in referenced class libraries
- middleware and policies can also live outside the host assembly
- contributing assemblies publish structured compile-time metadata
- the host remains the sole final composer of pipelines

This keeps the runtime simple while letting the generator build final pipelines for the full command universe visible to the host.

## Context lanes

Context lanes are available in `1.2.0`.
They allow independent, typed dispatcher pipelines inside the same application, where each lane has its own context, handlers, middleware and policies.

Use one lane by default. Add more lanes only when the application has real execution-context or pipeline differences.

See [Multi-Lane Dispatching](docs/multi-lane-dispatching.md) for documentation and Orders/Payments sample pointers.

## OpenTelemetry

Built-in OpenTelemetry support is available starting with `1.3.0-beta.1`.

TinyDispatcher emits standard .NET activities and metrics for dispatched commands and queries. Operations preserve the current trace context, so they appear beneath ASP.NET Core requests, worker activities, and parent dispatches.

```text
POST /orders
└── CreateOrderCommand
    └── ReserveInventoryCommand
```

Collection is opt-in through the application's OpenTelemetry configuration. TinyDispatcher does not select an exporter, send telemetry by itself, or automatically record command and query payloads.

See [OpenTelemetry](docs/opentelemetry.md) for registration, the telemetry contract, privacy behavior, and runnable samples.

## Generated application structure

Generated operation structure is available starting with `1.3.0-beta.2`. Generated pipeline
structure is available starting with `1.3.0-beta.3`:

```bash
dotnet add package TinyDispatcher --version 1.3.0-beta.3
```

TinyDispatcher exposes the command/query structure already discovered by its source generator:

```csharp
var operations = DispatcherPipelineBootstrap.GetOperations();
```

Each `DispatcherOperationStructure` identifies the operation type, handler type, command/query
kind, and optional dispatcher context. The snapshot is deterministic and independent from the
DI registration path. Reading it does not dispatch an operation, construct a handler, scan an
assembly, configure OpenTelemetry, or expose command/query payloads.

The generated structure is lazy: applications that never call `GetOperations()` do not build
the operation objects. The first read materializes and caches each generated contribution;
every read returns a fresh aggregate snapshot.

Applications can also opt into generated pipeline metadata and inspect the final composition
without dispatching operations or executing middleware:

```csharp
var pipelines = DispatcherPipelineMaps.Get();
```

See [Pipeline Maps](docs/pipeline-maps.md) for generator configuration and the metadata contract.

## Documentation

- [Getting Started](docs/getting-started.md)
- [Architecture](docs/architecture.md)
- [Multi-Assembly Composition](docs/multi-assembly-composition.md)
- [Multi-Lane Dispatching](docs/multi-lane-dispatching.md) (`1.2.0`)
- [OpenTelemetry](docs/opentelemetry.md)
- [Middleware](docs/middleware.md)
- [Pipelines & Layering](docs/pipelines.md)
- [Context & Features](docs/context.md)
- [Source Generator](docs/source-generator.md)
- [Pipeline Maps](docs/pipeline-maps.md)
- [TinySuite and sample app](docs/tiny-suite.md)
- [Performance Notes](docs/performance.md)
- [Migration Guide](docs/migration.md)
- [Design Decisions](docs/design-decisions.md)
- [Benchmarks](docs/benchmarks.md)

## Tiny suite

TinyDispatcher belongs to the Tiny suite:

| Project | Kind | Responsibility |
| --- | --- | --- |
| [TinyDispatcher](https://github.com/george2006/TinyDispatcher) | Library | Command and query execution |
| [TinyValidations](https://github.com/george2006/TinyValidations) | Library | Application input validation |
| [TinyEvents](https://github.com/george2006/TinyEvents) | Library | Reliable application-event handling through the outbox pattern |
| [TheTinyApplicationLayer](https://github.com/george2006/TheTinyApplicationLayer) | Example | Runnable ASP.NET Core and Blazor application using the complete suite |

The three libraries can be adopted independently. Using one does not require referencing the other two.

## When to use

TinyDispatcher is a good fit when you want:

- explicit execution flow you can read and debug
- deterministic middleware precedence
- compile-time discovery (no runtime scanning)
- a small, focused dispatch core rather than a full framework
- aot friendly.
- microservices, minimal Api and Azure functions.

## Test Coverage & Hardening

TinyDispatcher includes a comprehensive automated test suite covering both the runtime dispatcher and the Roslyn source generator.

Current coverage (March 2026):

| Component | Line Coverage | Branch Coverage |
|-----------|---------------|----------------|
| **Overall** | **~89%** | **~76%** |
| Runtime (`TinyDispatcher`) | ~84% | ~68% |
| Source Generator (`TinyDispatcher.SourceGen`) | ~90% | ~77% |

Critical runtime infrastructure is fully covered, including:

- `AppContext`
- `FeatureCollection`
- `DefaultAppContextFactory`
- `ServiceCollectionExtensions`
- `TinyBootstrap`

These components form the core execution and configuration model of TinyDispatcher.

## Samples

The repository contains runnable samples under `samples/`, including:

- ASP.NET and custom context setups
- context factory and closed-context middleware examples
- a multi-project sample showing cross-assembly handler and pipeline composition
- console and ASP.NET Core samples showing OpenTelemetry traces and metrics
