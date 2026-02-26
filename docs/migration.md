# Migration notes

This document is where breaking changes and migrations live.

## Middleware runtime simplification

Recent pipeline refactors may remove wrapper runtime types and have the pipeline implement the runtime interface directly.

If your middleware uses:

```csharp
Pipeline.ICommandPipelineRuntime<TCommand, TContext>
```

…no changes are required. The runtime contract remains stable.

## UseTinyNoOpContext bootstrap

If your application does not require a runtime context object, you can switch to:

```csharp
services.UseTinyNoOpContext(tiny => { });
```

Your command handlers should use `NoOpContext` as the context type.
