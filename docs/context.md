# Context and features

TinyDispatcher makes context explicit (`TContext`) for commands.

At runtime, a context instance is created **once per dispatch** using an `IContextFactory<TContext>`.
This keeps handlers clean (they receive an already-built context) and avoids "ambient" static state.

## Shipped AppContext

If you use `AppContext`, TinyDispatcher ships a default context factory.
It can populate features using `IFeatureInitializer` instances from DI.

This enables a pattern where your handlers depend on context features rather than directly on DI.

## Custom contexts

For ASP.NET, Azure Functions, or any environment where context depends on the current request/trigger, provide a context factory:

```csharp
services.UseTinyDispatcher<MyContext>(
    tiny => { },
    contextFactory: async (sp, ct) =>
    {
        // build MyContext from ambient state
        return await ValueTask.FromResult(new MyContext());
    });
```

Alternatively, register a factory in DI (useful when you want a dedicated type and unit tests):

```csharp
services.AddScoped<IContextFactory<MyContext>, MyContextFactory>();

services.UseTinyDispatcher<MyContext>(tiny =>
{
    // middleware, policies, features...
});
```

If no factory exists, TinyDispatcher fails fast at startup.
