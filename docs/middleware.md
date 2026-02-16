# Middleware

TinyDispatcher middleware is explicit and pipeline-driven.

Middleware receives a runtime object that advances the pipeline:

```csharp
ValueTask InvokeAsync(
    TCommand command,
    TContext context,
    Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
    CancellationToken ct);
```

To continue:

```csharp
await runtime.NextAsync(command, context, ct);
```

## Supported middleware shapes

TinyDispatcher supports middleware classes in two shapes.

### 1) Open middleware class (arity 2)

Reusable for any context:

```csharp
public sealed class LoggingMiddleware<TCommand, TContext>
    : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        // before
        await runtime.NextAsync(command, context, ct);
        // after
    }
}
```

### 2) Context-closed middleware class (arity 1)

Generic over the command only; the implemented interface fixes the context:

```csharp
public sealed class AuthorizationMiddleware<TCommand>
    : ICommandMiddleware<TCommand, AppContext>
    where TCommand : ICommand
{
    public ValueTask InvokeAsync(
        TCommand command,
        AppContext context,
        Pipeline.ICommandPipelineRuntime<TCommand, AppContext> runtime,
        CancellationToken ct)
        => runtime.NextAsync(command, context, ct);
}
```

## Registering middleware

Global middleware:

```csharp
tiny.UseGlobalMiddleware(typeof(LoggingMiddleware<,>));
```

Per-command middleware:

```csharp
tiny.UseMiddlewareFor<CreateOrder>(typeof(ValidationMiddleware<,>));
```

Policies (middleware applied to a group of commands) are documented in docs/pipelines.md.
