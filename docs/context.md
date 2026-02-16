# Context and features

TinyDispatcher makes context explicit (`TContext`) for commands.

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

If no factory exists, TinyDispatcher fails fast at startup.
