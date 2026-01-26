# TinyDispatcher

TinyDispatcher is a tiny, source-generator-friendly dispatcher for commands and queries.

This repository contains:

- `src/TinyDispatcher` - Runtime core (abstractions + dispatcher + DI entry points)
- `src/TinyDispatcher.SourceGen` - Incremental source generator
- `tests/TinyDispatcher.UnitTests` - Fast unit tests
- `tests/TinyDispatcher.IntegrationTests` - End-to-end wiring tests

## Key concepts

- Commands are context-aware: `ICommandHandler<TCommand, TContext>`
- Queries are context-less (for now): `IQueryHandler<TQuery, TResult>`
- The generator contributes handler maps (module initializer) and (optionally) pipeline DI registrations.

## Quick start (consumer)

1. Reference `TinyDispatcher` and add the source generator package.
2. Register the dispatcher:

```csharp
services.UseTinyDispatcher<MyContext>(tiny =>
{
    // tiny.UseGlobalMiddleware(typeof(LoggingMiddleware<,>));
    // tiny.UsePolicy<MyPolicy>();
});
```

