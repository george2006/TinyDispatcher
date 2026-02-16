# TinyDispatcher

TinyDispatcher is a small, compile-time oriented dispatcher for .NET.

It provides a predictable, explicit, and performant command/query dispatch core by moving:

- handler discovery to **build time**
- middleware pipeline composition to **generated code** (also build time)

…while keeping runtime execution simple and scope-friendly.

## Install

```bash
dotnet add package TinyDispatcher --prerelease
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

- **Getting Started**: docs/getting-started.md  
- **Middleware**: docs/middleware.md  
- **Pipelines & layering**: docs/pipelines.md  
- **Context & features**: docs/context.md  
- **Source generator & options**: docs/source-generator.md  
- **Pipeline maps**: docs/pipeline-maps.md  
- **Performance notes**: docs/performance.md  
- **Migration notes**: docs/migration.md  
- **Design decisions**: docs/design-decisions.md  

## When to use

TinyDispatcher is a good fit when you want:

- explicit execution flow you can read and debug
- deterministic middleware precedence
- compile-time discovery (no runtime scanning)
- a small, focused dispatch core rather than a full framework
