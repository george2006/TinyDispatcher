# TinyDispatcher

TinyDispatcher is a small, compile-time oriented dispatcher for .NET.

It provides a predictable, explicit, and performant command/query dispatch core by moving:

- handler discovery to **build time**
- middleware pipeline composition to **generated code** (also build time)

…while keeping runtime execution simple and scope-friendly.

## What you get

- **Compile-time handler discovery** (no runtime scanning/reflection)
- **Generated pipelines** (global middleware → policy middleware → per-command middleware → handler)
- **Deterministic ordering** and precedence rules (predictable output)
- **Explicit context (`TContext`)** for command handlers
- **Pluggable context factory** (pass a delegate or register `IContextFactory<TContext>`)
- **Feature-friendly `AppContext`** (optional `IFeatureInitializer`-based composition)
- **Source-generator diagnostics** for invalid shapes/config (fail fast, no guessing)

## Install

```bash
dotnet add package TinyDispatcher
```

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

Dispatch:

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
```

## Documentation

- [Getting Started](docs/getting-started.md)
- [Architecture](docs/architecture.md)
- [Middleware](docs/middleware.md)
- [Pipelines & Layering](docs/pipelines.md)
- [Context & Features](docs/context.md)
- [Source Generator](docs/source-generator.md)
- [Pipeline Maps](docs/pipeline-maps.md)
- [Performance Notes](docs/performance.md)
- [Migration Guide](docs/migration.md)
- [Design Decisions](docs/design-decisions.md)
- [Benchmarks](docs/benchmarks.md)

## When to use

TinyDispatcher is a good fit when you want:

- explicit execution flow you can read and debug
- deterministic middleware precedence
- compile-time discovery (no runtime scanning)
- a small, focused dispatch core rather than a full framework

## Samples

The repository contains small runnable samples under `samples/` (ASP.NET, custom contexts, context factories, closed-context middleware, etc.).
