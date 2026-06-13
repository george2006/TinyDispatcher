# Context, features, and lanes

TinyDispatcher makes context explicit (`TContext`) for commands.

At runtime, a context instance is created **once per dispatch** using an `IContextFactory<TContext>`.
This keeps handlers clean (they receive an already-built context) and avoids "ambient" static state.

The stable `1.1.x` line supports one typed context per `UseTinyDispatcher<TContext>` registration.
Context lanes are available in the `1.2.0` release candidate line.

## Shipped AppContext

If you use `AppContext`, TinyDispatcher ships a default context factory.
It can populate features using `IFeatureInitializer` instances from DI.

This enables a pattern where your handlers depend on context features rather than directly on DI.

## Custom contexts

For ASP.NET, Azure Functions, or any environment where context depends on the current request/trigger, provide a context factory.

The stable registration style is to register the factory in DI before calling `UseTinyDispatcher<TContext>`:

```csharp
public sealed class MyContextFactory : IContextFactory<MyContext>
{
    public ValueTask<MyContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(new MyContext());
    }
}

services.AddScoped<IContextFactory<MyContext>, MyContextFactory>();

services.UseTinyDispatcher<MyContext>(tiny =>
{
    // middleware, policies, features...
});
```

You can also pass a delegate factory directly:

```csharp
services.UseTinyDispatcher<MyContext>(
    tiny => { },
    contextFactory: async (sp, ct) =>
    {
        // build MyContext from ambient state
        return await ValueTask.FromResult(new MyContext());
    });
```

If no factory exists, TinyDispatcher fails fast at startup.

## Context lanes

Context lanes are part of `1.2.0-rc*`.
They provide independent, typed dispatcher pipelines inside the same application, where each lane has its own context, handlers, middleware and policies.

Use one lane by default. Add more lanes only when the application has real execution-context or pipeline differences.

A lane is a typed dispatcher pipeline for a specific execution context:

- an Orders lane can use `OrdersContext`
- a Payments lane can use `PaymentsContext`
- an application-wide lane can use `TinyDispatcher.AppContext`
- a no-context lane can use `NoOpContext`

Each lane can have its own context factory, command handlers, global middleware, per-command middleware, policies, and generated pipeline. For the full Orders/Payments release candidate walkthrough, see [Multi-Lane Dispatching](multi-lane-dispatching.md).

## No-op context

If you do not need any context at runtime, you can bootstrap with a no-op context:

```csharp
services.UseTinyNoOpContext(tiny =>
{
    // optional: middleware, policies
});
```

This uses `NoOpContext` (a zero-payload struct). It is intended for maximum throughput.
