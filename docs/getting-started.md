# Getting started

This guide shows the minimum steps to wire TinyDispatcher into an application.

## 1) Install

```bash
dotnet add package TinyDispatcher
```

## 2) Define commands, queries and handlers

Commands implement `ICommand`:

```csharp
public sealed record CreateOrder(string OrderId) : ICommand;
```

Handlers are context-aware via `TContext`:

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
{
    public Task HandleAsync(CreateOrder command, AppContext ctx, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

Queries are supported too (queries are currently context-less):

```csharp
public sealed record GetOrder(string OrderId) : IQuery<OrderDto>;

public sealed class GetOrderHandler : IQueryHandler<GetOrder, OrderDto>
{
    public Task<OrderDto> HandleAsync(GetOrder query, CancellationToken ct = default)
        => Task.FromResult(new OrderDto(query.OrderId));
}
```

## 3) Register

### Option A: Use the shipped `AppContext`

```csharp
services.UseTinyDispatcher<AppContext>(tiny =>
{
    // optional: middleware, policies, features
});
```

### Option B: Use a custom context with a factory

For a custom context, implement `IContextFactory<TContext>` and register it in DI before calling `UseTinyDispatcher<TContext>`:

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
    // optional: middleware, policies, features
});
```

If no context factory is provided and none is registered, TinyDispatcher fails fast at startup.

### Option C: Pass a delegate factory

For simple cases, you can pass a factory delegate directly:

```csharp
services.UseTinyDispatcher<MyContext>(
    tiny => { },
    contextFactory: async (sp, ct) =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return await ValueTask.FromResult(new MyContext(http?.TraceIdentifier));
    });
```

### Option D: No-op context (when you don't need one)

If your commands do not need a runtime context object, bootstrap with a no-op context:

```csharp
services.UseTinyNoOpContext(tiny =>
{
    // optional: middleware, policies
});
```

In this mode, command handlers use `NoOpContext`:

```csharp
public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, NoOpContext>
{
    public Task HandleAsync(CreateOrder command, NoOpContext ctx, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

## 4) Add policies and middleware

### Policy (group commands + attach middleware)

Register a policy in the bootstrap:

```csharp
services.UseTinyDispatcher<AppContext>(tiny =>
{
    tiny.UsePolicy<CheckoutPolicy>();
});
```

Define the policy in code using attributes:

```csharp
[TinyPolicy]
[UseMiddleware(typeof(PolicyLoggingMiddleware<,>))]
[UseMiddleware(typeof(PolicyValidationMiddleware<,>))]
[ForCommand(typeof(CreateOrder))]
[ForCommand(typeof(CancelOrder))]
public sealed class CheckoutPolicy { }
```

### Global middleware (applies to ALL commands)

```csharp
services.AddTransient(typeof(GlobalLoggingMiddleware<,>));

services.UseTinyDispatcher<AppContext>(tiny =>
{
    tiny.UseGlobalMiddleware(typeof(GlobalLoggingMiddleware<,>));
});
```

### Per-command middleware (applies to ONE command)

```csharp
services.AddTransient(typeof(OnlyForPayMiddleware<,>));

services.UseTinyDispatcher<AppContext>(tiny =>
{
    tiny.UseMiddlewareFor<Pay>(typeof(OnlyForPayMiddleware<,>));
});
```

### “Open” vs “closed-context” middleware

- **Open** (generic on `TCommand` + `TContext`): `MyMiddleware<TCommand, TContext>`
- **Closed-context** (generic only on `TCommand`, context fixed): `MyMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>`

Example closed-context middleware:

```csharp
services.AddTransient(typeof(RequestIdMiddleware<>));

services.UseTinyDispatcher<AppContext>(tiny =>
{
    tiny.UseGlobalMiddleware(typeof(RequestIdMiddleware<>));
});

public sealed class RequestIdMiddleware<TCommand> : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public Task InvokeAsync(
        TCommand command,
        AppContext ctx,
        CommandDelegate<TCommand, AppContext> next,
        CancellationToken ct)
        => next(command, ctx, ct);
}
```

## 5) Dispatch

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
```

```csharp
var dto = await dispatcher.DispatchAsync<GetOrder, OrderDto>(new GetOrder("123"), ct);
```
