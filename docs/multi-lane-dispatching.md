# Multi-lane dispatching

TinyDispatcher supports context lanes: independent, typed dispatcher pipelines inside the same application. Each lane has its own context, handlers, middleware and policies.

A lane is a typed dispatcher pipeline for a specific execution context. In a modular monolith, that usually means one lane per business area when those areas really do execute differently.

For a runnable version of this idea, see:

- [Program.cs](../samples/src/TinyDispatcher.Samples.MultiLaneDispatching/Program.cs)
- [OrdersModule.cs](../samples/src/TinyDispatcher.Samples.MultiLaneDispatching/Orders/OrdersModule.cs)
- [PaymentsModule.cs](../samples/src/TinyDispatcher.Samples.MultiLaneDispatching/Payments/PaymentsModule.cs)

## The idea

Don't put every command into one flat application pipeline. Use context lanes to give each business area its own typed execution path.

For example:

- the Orders lane uses `OrdersContext`
- the Payments lane uses `PaymentsContext`
- an application-wide lane can use `TinyDispatcher.AppContext`
- a no-context lane can use `NoOpContext`

Each lane can have:

- its own context
- its own context factory
- its own command handlers
- its own global middleware
- its own per-command middleware
- its own policies
- its own generated pipeline

Context lanes are not microservices. They are local, in-process execution lanes for modular monoliths. TinyDispatcher remains small: lanes do not add messaging, persistence, retries, sagas, orchestration, or distributed guarantees.

Use one lane by default. Add more lanes only when the application has real execution-context or pipeline differences.

## Orders and Payments

A host can compose module-owned lane registrations:

```csharp
services.AddOrdersLane();
services.AddPaymentsLane();
```

Each module then owns the setup for its lane. Conceptually, the Orders lane might look like this:

```csharp
services.UseTinyDispatcher<OrdersContext>(tiny =>
{
    tiny.UseFactory<OrdersContextFactory>();
    tiny.UseGlobalMiddleware(typeof(ConsoleLogMiddleware<,>));
    tiny.UseMiddlewareFor<SubmitOrder>(typeof(OrderValidationMiddleware<,>));
    tiny.UsePolicy<OrderApprovalPolicy>();
});
```

And the Payments lane can make different choices:

```csharp
services.UseTinyDispatcher<PaymentsContext>(tiny =>
{
    tiny.UseFactory<PaymentsContextFactory>();
    tiny.UseGlobalMiddleware(typeof(ConsoleLogMiddleware<,>));
    tiny.UseMiddlewareFor<CapturePayment>(typeof(PaymentAuditMiddleware<,>));
    tiny.UsePolicy<PaymentRiskPolicy>();
});
```

Both lanes live in the same process. The difference is that their execution paths are typed and registered separately.

## Dispatching

Resolve the dispatcher for the lane you want to execute:

```csharp
var orders = provider.GetRequiredService<IDispatcher<OrdersContext>>();
await orders.DispatchAsync(new SubmitOrder("ORD-1001"));

var payments = provider.GetRequiredService<IDispatcher<PaymentsContext>>();
await payments.DispatchAsync(new CapturePayment("PAY-2001", 42.50m));
```

Orders commands run through the Orders lane. Payments commands run through the Payments lane.

That means Orders middleware and policies do not become a global bag applied to everything. Payments can have its own execution context and risk/audit pipeline. The host composes the lanes, but each module owns its own registration.

## When to use context lanes

Use context lanes when:

- different modules need different context data
- different modules need different middleware or policies
- a modular monolith has clear business areas
- you want compile-time typed boundaries around command execution
- a single global application context would become too large

Good examples are Orders and Payments modules where Orders needs tenant/order approval behavior while Payments needs merchant/risk/audit behavior.

## When not to use context lanes

Do not use multiple lanes when:

- one application context is enough
- all commands share the same execution pipeline
- direct DI or a single dispatcher registration is simpler
- the separation would only add ceremony

A single lane is the right default. If every command uses the same context and the same pipeline, keep the registration simple.

## Compared with a flat mediator-style pipeline

Flat mediator-style pipeline:

- one global mediator
- one global application pipeline
- easy to accidentally apply behavior too broadly
- module boundaries often exist only by convention

TinyDispatcher context lanes:

- one typed dispatcher per lane
- lane-specific context
- lane-specific middleware and policies
- module boundaries visible in code
- still one process, still a modular monolith

This is not an argument against mediator-style tools. A flat pipeline can be exactly right for small systems or applications with one natural execution context. Context lanes are useful when a modular monolith has multiple business areas that deserve different typed execution paths.

## Running the sample

The runnable sample is under `samples/src/TinyDispatcher.Samples.MultiLaneDispatching`.

```powershell
dotnet run --project samples\src\TinyDispatcher.Samples.MultiLaneDispatching\TinyDispatcher.Samples.MultiLaneDispatching.csproj
```

It shows four lanes in one process:

- Orders with `OrdersContext`
- Payments with `PaymentsContext`
- a default `TinyDispatcher.AppContext` lane
- a no-context `NoOpContext` lane
