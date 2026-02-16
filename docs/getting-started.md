# Getting started

This guide shows the minimum steps to wire TinyDispatcher into an application.

## 1) Define commands, queries and handlers

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

## 2) Register

### Option A: Use the shipped `AppContext`

```csharp
services.UseTinyDispatcher<AppContext>(tiny =>
{
    // optional: middleware, policies, features
});
```

### Option B: Use a custom context with a factory

```csharp
services.UseTinyDispatcher<MyContext>(
    tiny => { },
    contextFactory: async (sp, ct) =>
    {
        var http = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
        return await ValueTask.FromResult(new MyContext(http?.TraceIdentifier));
    });
```

If no context factory is provided and none is registered, TinyDispatcher fails fast at startup.

## 3) Dispatch

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
```

```csharp
var dto = await dispatcher.DispatchAsync<GetOrder, OrderDto>(new GetOrder("123"), ct);
```
