# TinyDispatcher

> **TinyDispatcher** is a small, compile-time oriented dispatcher for .NET.
>
> It provides a **predictable, explicit, and performant** command/query dispatch core by moving:
> - handler discovery to **build time**, and
> - middleware pipeline composition to **generated code** (also build time),
> while keeping runtime execution simple and scope-friendly.

TinyDispatcher is intentionally **not** a full CQRS framework.
It is a focused building block you can explain on a whiteboard, debug easily, and run in production.

It is designed for teams that want a **small, explicit dispatch core** without buying into a full framework ecosystem.

This README is split into two parts:

1. **How to use it** — minimal and consumer-facing  
2. **How it is built** — architecture, generated code, shipped defaults, and seams

---

# 1. How to use TinyDispatcher

This section is deliberately short.

## 1.1 Define commands and handlers

Commands implement `ICommand`:

```csharp
public sealed record CreateOrder(string OrderId) : ICommand;
```

Handlers are **context-aware** (`TContext` is explicit):

```csharp
public sealed class CreateOrderHandler
    : ICommandHandler<CreateOrder, AppContext>
{
    public Task HandleAsync(CreateOrder command, AppContext ctx, CancellationToken ct = default)
    {
        // business logic
        return Task.CompletedTask;
    }
}
```

Queries are supported too (no context for now):

```csharp
public sealed record GetOrder(string OrderId) : IQuery<OrderDto>;

public sealed class GetOrderHandler : IQueryHandler<GetOrder, OrderDto>
{
    public Task<OrderDto> HandleAsync(GetOrder query, CancellationToken ct = default)
        => Task.FromResult(new OrderDto(query.OrderId));
}
```

---

## 1.2 Register TinyDispatcher

### Quick start: use the shipped `AppContext` (no factory needed)

If you use `AppContext`, TinyDispatcher ships a default context factory that can populate features
using `IFeatureInitializer` instances from DI.

```csharp
services.UseTinyDispatcher<AppContext>(tiny =>
{
    // optional: middleware, policies, features
});
```

### Use your own context type

If you use a custom `TContext`, provide a factory (typical for ASP.NET / Azure Functions):

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

---

## 1.3 Source generator configuration (recommended)

TinyDispatcher is finalized at **build time** by a source generator.

The recommended way to configure generation behavior is via an
**assembly-level attribute** in your application:

```csharp
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    GeneratedNamespace = "MyApp.Dispatcher.Generated",
    IncludeNamespacePrefix = "MyApp.",
    EmitPipelineMap = true,
    PipelineMapFormat = "json"
)]
```

## 1.4 Dispatch

Commands:

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
var dto = await dispatcher.DispatchAsync<GetOrder, OrderDto>(new GetOrder("123"), ct);
```

Queries:

```csharp
await dispatcher.DispatchAsync(new CreateOrder("123"), ct);
var dto = await dispatcher.DispatchAsync<GetOrder, OrderDto>(new GetOrder("123"), ct);
```

---

## 1.5 Middleware and policies (optional)

Global middleware:

```csharp
tiny.UseGlobalMiddleware(typeof(LoggingMiddleware<,>));
```

Per-command middleware:

```csharp
tiny.UseMiddlewareFor<CreateOrder>(typeof(ValidationMiddleware<,>));
```

Policies:

```csharp
[TinyPolicy]
[UseMiddleware(typeof(ValidationMiddleware<,>))]
[UseMiddleware(typeof(LoggingMiddleware<,>))]
[ForCommand(typeof(CreateOrder))]
[ForCommand(typeof(CancelOrder))]
public sealed class CheckoutPolicy { }
```

```csharp
tiny.UsePolicy<CheckoutPolicy>();
```

---

## 1.6 Shipped context: add features (optional)

`AppContext` ships with a small feature system. Add initializers:

```csharp
public sealed class RequestInfo
{
    public string? CorrelationId { get; init; }
}

public sealed class RequestInfoInitializer : IFeatureInitializer
{
    private readonly IHttpContextAccessor _http;
    public RequestInfoInitializer(IHttpContextAccessor http) => _http = http;

    public void Initialize(IFeatureCollection features)
    {
        features.Add(new RequestInfo { CorrelationId = _http.HttpContext?.TraceIdentifier });
    }
}
```

Register it:

```csharp
services.AddHttpContextAccessor();

services.UseTinyDispatcher<AppContext>(tiny =>
{
    tiny.AddFeatureInitializer<RequestInfoInitializer>();
});
```

Use it inside handlers:

```csharp
public Task HandleAsync(CreateOrder command, AppContext ctx, CancellationToken ct = default)
{
    var req = ctx.GetFeature<RequestInfo>();
    Console.WriteLine(req.CorrelationId);
    return Task.CompletedTask;
}
```

That’s all you need to use TinyDispatcher.

Everything below explains **what it builds for you** and **why**.

---

# 2. How TinyDispatcher is built

This section is written for senior engineers, principals, and reviewers.

---

## 2.1 Design goals

TinyDispatcher is built around a few strict principles:

- **Compile-time over runtime** whenever possible
- **Deterministic behavior** over DI-order-based behavior
- **Explicit scoping** instead of ambient/static state
- **Readable generated code** as a first-class feature


---

## 2.2 Runtime core (what exists at runtime)

The runtime core provides:

- Message abstractions: `ICommand`, `IQuery<TResult>`
- Handlers: `ICommandHandler<TCommand,TContext>`, `IQueryHandler<TQuery,TResult>`
- Context factory: `IContextFactory<TContext>`
- Middleware: `ICommandMiddleware<TCommand,TContext>` + `CommandDelegate<TCommand,TContext>`
- Dispatcher: `IDispatcher<TContext>`
- Registry: `IDispatcherRegistry` (materialized from compile-time contributions)

Additionally, the core includes an **optional shipped default context**:

- `AppContext` + `FeatureCollection`
- `IFeatureInitializer`
- `DefaultAppContextFactory` (builds an `AppContext` per dispatch)

This means you can start with `AppContext` immediately, and later swap to your own `TContext`
without changing the dispatch model.

---

## 2.3 Pipeline Maps (compile-time introspection)

TinyDispatcher can generate **compile-time pipeline maps** that describe *exactly* what will execute for each command.

These maps are produced by the source generator from the **same information used to generate the pipelines themselves**, which means:

> **The map is not an approximation.  
> It is the truth.**

---

### What is a Pipeline Map?

For every discovered command, TinyDispatcher can emit a **pipeline descriptor** that includes:

- The command type
- The context type
- The final handler
- The ordered list of middlewares **as they execute**
- The policies applied (if any)
- The **origin** of each middleware:
  - `Global`
  - `Policy:<PolicyType>`
  - `Command:<CommandType>`

Example (JSON payload embedded in generated source):

```json
{
  "command": "global::MyApp.Payments.CreatePayment",
  "context": "global::MyApp.CheckoutContext",
  "handler": "global::MyApp.Payments.CreatePaymentHandler",
  "policies": [
    "global::MyApp.Policies.CheckoutPolicy"
  ],
  "middlewares": [
    {
      "type": "global::MyApp.Middleware.CorrelationMiddleware",
      "source": "Global"
    },
    {
      "type": "global::MyApp.Middleware.AuthorizationMiddleware",
      "source": "Policy:global::MyApp.Policies.CheckoutPolicy"
    },
    {
      "type": "global::MyApp.Middleware.ValidationMiddleware",
      "source": "Command:global::MyApp.Payments.CreatePayment"
    }
  ]
}
```

---

### Why compile-time?

Most frameworks can *execute* pipelines, but few can **explain them**.

TinyDispatcher generates pipeline maps at **compile time**, which guarantees that:

- The map reflects the **exact pipeline that will run**
- There is no runtime cost
- No reflection or diagnostics APIs are involved
- Pipelines are **visible, reviewable, and testable**

This makes pipelines:

- Easier to reason about
- Easier to review in PRs
- Easier to validate in tests
- Easier to document

---

### How pipeline maps are generated

Pipeline maps are emitted by the source generator as **generated C# files** (`*.g.cs`).

The actual map data (JSON / Mermaid) is embedded inside comment blocks:

```csharp
// <auto-generated/>
#nullable enable

/*
TINYDISPATCHER_PIPELINE_MAP_JSON
{
  ...
}
*/
```

This approach ensures that:

- The artifacts are reliably emitted by the compiler
- They appear under *Generated Documents* in IDEs
- They can be written to `obj/` when `EmitCompilerGeneratedFiles` is enabled
- No runtime types or APIs are polluted

---

### Enabling pipeline maps

Pipeline maps are **opt-in**.

In the consuming project:

```xml
<PropertyGroup>
  <TinyDispatcher_GeneratePipelineMap>true</TinyDispatcher_GeneratePipelineMap>
  <TinyDispatcher_PipelineMapFormat>json;mermaid</TinyDispatcher_PipelineMapFormat>
</PropertyGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="TinyDispatcher_GeneratePipelineMap" />
  <CompilerVisibleProperty Include="TinyDispatcher_PipelineMapFormat" />
</ItemGroup>
```

To persist generated files to disk:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>
    $(BaseIntermediateOutputPath)Generated
  </CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

---

### Supported formats

- **JSON** – for tooling, CI validation, tests, or custom UIs
- **Mermaid** – for documentation and visual inspection

Both formats are derived from the same compile-time pipeline model.

---

### What makes this different?

- Pipelines are **per-command**, not global guesses
- Middleware order is **explicit and deterministic**
- Every middleware includes **why it is there**
- The map reflects **generated code**, not runtime behavior

In TinyDispatcher:

> **What runs is visible.  
> And what is visible can be verified.**

---

## 2.4 Compile-time discovery (no runtime scanning)

At build time, a source generator:

- discovers all `ICommandHandler<TCommand, TContext>` implementations
- discovers all `IQueryHandler<TQuery, TResult>` implementations
- validates that there are no duplicate handlers
- emits static handler maps per assembly

### Generated handler contribution (simplified)

```csharp
internal sealed class ThisAssemblyContribution : IMapContribution
{
    public IEnumerable<KeyValuePair<Type, Type>> CommandHandlers =>
        new[]
        {
            new(typeof(CreateOrder), typeof(CreateOrderHandler))
        };

    public IEnumerable<KeyValuePair<Type, Type>> QueryHandlers =>
        new[]
        {
            new(typeof(GetOrder), typeof(GetOrderHandler))
        };
}
```

Each assembly contributes its map via a module initializer.
At startup, TinyDispatcher builds a single immutable registry.

**Result:** no reflection, no scanning, no runtime guessing.

---

## 2.5 Context per dispatch (explicit scoping)

Every command dispatch creates a fresh context:

```csharp
var ctx = await contextFactory.CreateAsync(ct);
```

Why this matters:

- request-scoped services are safe to use
- no cross-request leakage
- consistent behavior across ASP.NET, Azure Functions, and workers

### Shipped `AppContext` factory

If `TContext == AppContext` and you did not provide a factory,
`UseTinyDispatcher<AppContext>` registers a default scoped factory that runs all `IFeatureInitializer`s:

```csharp
public sealed class DefaultAppContextFactory : IContextFactory<AppContext>
{
    private readonly IEnumerable<IFeatureInitializer> _initializers;

    public ValueTask<AppContext> CreateAsync(CancellationToken ct = default)
    {
        var features = new FeatureCollection();

        foreach (var init in _initializers)
            init.Initialize(features);

        return new ValueTask<AppContext>(new AppContext(features));
    }
}
```

**Important:** initializers run **per dispatch**, by design, because they can rely on scoped services
(e.g., `IHttpContextAccessor`).

---

## 2.6 Middleware and pipeline model

TinyDispatcher does **not** compose pipelines dynamically at runtime.

Instead, it generates concrete pipeline classes at build time.

There are three pipeline types:

| Pipeline type | Interface | Precedence |
|--------------|-----------|------------|
| Per-command | `ICommandPipeline<TCommand, TContext>` | Highest |
| Policy | `IPolicyCommandPipeline<TCommand, TContext>` | Medium |
| Global | `IGlobalCommandPipeline<TCommand, TContext>` | Lowest |

If none exists, the handler is invoked directly.

---

## 2.7 Explicit pipeline precedence (implemented in the dispatcher)

Pipeline resolution is deterministic and does not rely on DI ordering:

```text
Per-command pipeline
    ↓
Policy pipeline
    ↓
Global pipeline
    ↓
Direct handler invocation
```

Conceptually, the dispatcher resolves and caches the “best” pipeline per command type like this:

```csharp
// 1) ICommandPipeline<TCommand,TContext>       (per-command override)
// 2) IPolicyCommandPipeline<TCommand,TContext> (policy)
// 3) IGlobalCommandPipeline<TCommand,TContext> (global)
```

This avoids an entire class of bugs where changing registration order silently changes behavior.

---

## 2.8 How pipelines are generated

The generator emits different artifacts depending on what it discovers.

### Always emitted (per consumer project with handlers)

- A module initializer that contributes the assembly’s maps
- A `ThisAssemblyContribution` map for handlers
- A pipeline contribution hook (so the host can apply pipeline DI registrations)

### Host-gated emission (only in the host)

Pipeline implementations are generated **only in the host** project when the generator detects:
- at least one call to `services.UseTinyDispatcher<TContext>(...)`

This avoids generating host-only artifacts in libraries that merely define handlers.

### Context type inference

If you don’t explicitly configure the context type for generation,
the generator can infer it from the `UseTinyDispatcher<TContext>` call-site.

---

## 2.9 Generated pipeline examples

These examples are simplified but representative of the shape of the generated code.

### Global pipeline (open generic)

```csharp
internal sealed class TinyDispatcherGlobalPipeline<TCommand>
    : IGlobalCommandPipeline<TCommand, AppContext>
    where TCommand : ICommand
{
    private readonly IServiceProvider _sp;

    public Task ExecuteAsync(
        TCommand command,
        AppContext ctx,
        ICommandHandler<TCommand, AppContext> handler,
        CancellationToken ct)
    {
        CommandDelegate<TCommand, AppContext> next = handler.HandleAsync;

        // Global middleware (outer)
        next = Logging(next);
        next = Metrics(next);

        return next(command, ctx, ct);
    }

    private CommandDelegate<TCommand, AppContext> Logging(CommandDelegate<TCommand, AppContext> inner)
        => (cmd, ctx, ct) => _sp.GetRequiredService<LoggingMiddleware<TCommand, AppContext>>()
                               .InvokeAsync(cmd, ctx, inner, ct);
}
```

### Per-command pipeline (closed generic, highest precedence)

```csharp
internal sealed class TinyDispatcherPipeline_CreateOrder
    : ICommandPipeline<CreateOrder, AppContext>
{
    private readonly IServiceProvider _sp;

    public Task ExecuteAsync(
        CreateOrder command,
        AppContext ctx,
        ICommandHandler<CreateOrder, AppContext> handler,
        CancellationToken ct)
    {
        CommandDelegate<CreateOrder, AppContext> next = handler.HandleAsync;

        // Per-command middleware (inner)
        next = Validation(next);

        // Global middleware (outer)
        next = Logging(next);

        return next(command, ctx, ct);
    }
}
```

### Policy pipeline (open by policy, reused across commands)

```csharp
internal sealed class TinyDispatcherPolicyPipeline_CheckoutPolicy<TCommand>
    : IPolicyCommandPipeline<TCommand, AppContext>
    where TCommand : ICommand
{
    private readonly IServiceProvider _sp;

    public Task ExecuteAsync(
        TCommand command,
        AppContext ctx,
        ICommandHandler<TCommand, AppContext> handler,
        CancellationToken ct)
    {
        CommandDelegate<TCommand, AppContext> next = handler.HandleAsync;

        // Policy middleware (inner)
        next = Validation(next);

        // Global middleware (outer)
        next = Logging(next);

        return next(command, ctx, ct);
    }
}
```

Key characteristics:

- plain C# (no reflection or expression trees on the hot path)
- DI resolution happens inside the active scope (`IServiceProvider`)
- ordering is deterministic (captured from call sites)

---

## 2.10 How pipeline DI registrations are applied

Consumer assemblies contribute pipeline registrations into a store.
At startup, the host calls a single apply step:

```csharp
DispatcherPipelineBootstrap.Apply(services);
```

This is how the host pulls in generated pipelines without manually registering each one.

---

## 2.11 Extension seams

TinyDispatcher is intentionally composable:

- replace the shipped `AppContext` with your own `TContext`
- replace the context factory (`IContextFactory<TContext>`)
- use only global middleware, only policies, only per-command, or none
- ignore source-generated pipelines entirely if you want direct handler invocation

It is designed as a core primitive, not a framework trap.

---

## 2.12 When not to use TinyDispatcher

Do **not** use TinyDispatcher if you need:

- full CQRS infrastructure (projections, read models, outbox, etc.)
- dynamic runtime handler loading
- heavy ecosystem integrations out of the box

Use it when you want a **clean, explicit dispatch core** that can be reviewed quickly
and behaves deterministically.

---

## What TinyDispatcher is NOT

TinyDispatcher is intentionally small and opinionated.  
Understanding what it **does not try to be** is as important as understanding what it does.

### ❌ Not a replacement for MediatR

TinyDispatcher does **not** aim to replace MediatR or compete with its ecosystem.

MediatR provides a rich, runtime-composed pipeline model with dynamic behaviors and strong community adoption. TinyDispatcher explores a different set of trade-offs: compile-time discovery instead of runtime scanning, generated pipelines instead of dynamically composed behaviors, and explicit pipeline precedence instead of DI-order-based behavior.

If you are happy with MediatR’s runtime model and behavior pipeline, TinyDispatcher does not try to convince you otherwise.

---

### ❌ Not a full CQRS framework

TinyDispatcher does not provide projections, event sourcing, sagas, process managers, inbox/outbox patterns, or resiliency primitives.

It focuses only on **dispatching commands and queries predictably**, leaving higher-level architectural concerns to the surrounding system.

---

### ❌ Not a runtime plugin or extension system

TinyDispatcher is not designed for dynamic composition.

Handlers, pipelines, and middleware are expected to be known at build time. You cannot load handlers dynamically, enable or disable pipelines at runtime, or alter execution flow via reflection.

Behavior is generated and fixed at compile time by design.

---

### ❌ Not a “magic” abstraction layer

TinyDispatcher does not hide execution flow.

There are no implicit behaviors, hidden interception chains, or opaque runtime graphs. If you want execution to be intentionally abstracted away, TinyDispatcher is not a good fit.

---

### ❌ Not a zero-cost abstraction

TinyDispatcher is not a zero-cost abstraction, but it is designed to **remove avoidable runtime work**.

Unlike runtime-composed dispatchers, TinyDispatcher does not scan assemblies, build handler graphs, or compose middleware pipelines at startup. Discovery and pipeline composition happen at build time, and the runtime executes only pre-generated code.

In cold-start–sensitive environments such as Azure Functions or background workers, this reduces startup work and improves time-to-first-dispatch compared to dispatchers that rely heavily on runtime reflection and dynamic composition.

TinyDispatcher still pays the unavoidable costs of dependency injection (container setup, scoped resolution, context creation). The goal is not zero overhead, but ensuring that **only unavoidable work happens at runtime**.

---

### ❌ Not a general-purpose framework

TinyDispatcher is a **core building block**, not a platform.

It intentionally avoids large configuration surfaces, heavy conventions, and framework-level opinions. It is meant to be composed into an architecture, not to define it.

---

## What TinyDispatcher is good at

TinyDispatcher is designed for teams and systems that value **explicitness, predictability, and debuggability** over maximum runtime flexibility.

### ✔️ Explicit execution flow

The execution path of a command is explicit and readable.

Middleware order, pipeline selection, and handler invocation are all visible in generated code. There are no implicit behaviors or hidden interception chains, making production behavior easy to reason about.

---

### ✔️ Deterministic middleware precedence

Pipeline precedence is explicit and enforced by the dispatcher itself.

Per-command pipelines override everything, policy pipelines apply next, global pipelines apply last, and if no pipeline exists the handler is invoked directly. This avoids subtle bugs caused by DI registration order and makes behavior consistent across environments.

---

### ✔️ Compile-time safety

Handler discovery and validation happen at build time.

Duplicate handlers, ambiguous registrations, and invalid configurations are detected early, before the application starts, shifting failure to the safest possible moment: compilation.

---

### ✔️ Reduced startup work

By moving discovery and pipeline composition to build time, TinyDispatcher minimizes unnecessary startup work.

This is especially valuable in Azure Functions, background workers, and other cold-start–sensitive services where time-to-first-execution matters.

---

### ✔️ Context as a first-class concept

Commands always execute with an explicit context created per dispatch.

This makes request-scoped data, correlation information, feature flags, and environment-specific state explicit, testable, and free from ambient or static dependencies.

---

### ✔️ Generated code you can read and debug

Generated pipelines are plain C# code.

They can be inspected, debugged step by step, and understood without learning a custom DSL or runtime abstraction model.

---

### ✔️ Small surface area, easy to reason about

TinyDispatcher keeps its API surface intentionally small.

It focuses on doing one thing well — dispatching commands and queries predictably — and composes cleanly with existing architectures rather than trying to replace them.
🚧 Project Status

**Version:** `0.1.0`  
**Status:** early preview / experimental

TinyDispatcher is usable today, but still evolving.
Expect possible breaking changes between minor versions while `0.x`.
Feedback and PRs are welcome.

---

## Assembly-level configuration (important)

TinyDispatcher is configured at **assembly level**.

`UseTinyDispatcher<TContext>(...)` is intended to be called **once per assembly**, typically during application startup.

### Why this matters

TinyDispatcher uses **compile-time discovery** (source generators / analyzers) to build command pipelines.  
Because of this, the generator analyzes the **entire assembly**.

All `UseTinyDispatcher(...)` calls found in the same assembly are treated as contributions to a **single application configuration**.

This means:

- Multiple `UseTinyDispatcher(...)` calls do **not** create isolated configurations
- They are **merged** at compile time
- Middleware and pipeline contributions may affect commands outside their apparent scope

This behavior is powerful, but can be surprising if not understood.

### Rule of thumb

> **Assembly = Application**

If you need isolated configurations (for example, in tests or samples), use **separate projects / assemblies**.

---

## Final note

TinyDispatcher is meant to be read.

If someone cannot understand what happens to a command by reading the generated code,
then the design has failed.

That principle guided every decision in this project.

