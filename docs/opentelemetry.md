# OpenTelemetry

TinyDispatcher provides built-in traces and metrics through the standard .NET diagnostics APIs.
It does not choose an exporter or send telemetry anywhere. Applications decide whether to collect the telemetry and where to export it.

This capability is available starting with `1.3.0-beta.1`.

Install the beta with:

```powershell
dotnet add package TinyDispatcher --version 1.3.0-beta.1
```

## Registration

Register the TinyDispatcher activity source and meter with OpenTelemetry:

```csharp
builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(TinyDispatcherTelemetry.ActivitySourceName))
    .WithMetrics(metrics => metrics
        .AddMeter(TinyDispatcherTelemetry.MeterName));
```

Add the exporter and any host instrumentation required by the application. For example, an ASP.NET Core application can add its request instrumentation so dispatched operations appear beneath the request that caused them:

```csharp
.WithTracing(tracing => tracing
    .AddAspNetCoreInstrumentation()
    .AddSource(TinyDispatcherTelemetry.ActivitySourceName)
    .AddOtlpExporter())
```

No TinyDispatcher activities are recorded when nothing listens to its activity source. Metrics are not collected unless the application registers its meter.

## Traces

TinyDispatcher creates one internal activity for each dispatched command or query. The activity uses the current `Activity` as its parent, including activities started by ASP.NET Core, a worker, or application code.

Nested dispatches retain their natural trace hierarchy:

```text
POST /orders
└── CreateOrderCommand
    └── ReserveInventoryCommand
```

Each operation activity records:

| Attribute | Description |
| --- | --- |
| `tiny.operation.name` | Short operation type name |
| `tiny.operation.identity` | Fully qualified operation type name |
| `tiny.operation.type` | `command` or `query` |
| `tiny.operation.handler` | Fully qualified handler type name |
| `tiny.operation.outcome` | `success`, `failure`, or `canceled` |

A failure sets the activity status to `Error` and records the exception using the standard OpenTelemetry exception attributes. Cooperative cancellation records the `canceled` outcome without converting it into a failure.

## Metrics

The `TinyDispatcher` meter publishes:

| Instrument | Type | Unit | Description |
| --- | --- | --- | --- |
| `tiny.operation.executions` | Counter | `{operation}` | Number of completed operations |
| `tiny.operation.duration` | Histogram | `s` | Operation execution duration in seconds |

Both instruments use these dimensions:

- `tiny.operation.identity`
- `tiny.operation.type`
- `tiny.operation.outcome`

Histogram aggregation and bucket boundaries belong to the collecting application or backend. The telemetry samples configure boundaries suitable for operations that usually complete in milliseconds.

## Payload privacy

TinyDispatcher does not automatically record command or query payloads. Application values such as customer identifiers, email addresses, order numbers, and request bodies are therefore not added to the telemetry contract.

Applications can add business-safe attributes in their own activities when required. Sensitive or high-cardinality values should not be added to metric dimensions.

## Samples

- `samples/src/TinyDispatcher.Samples.Telemetry.Console` demonstrates commands, queries, nested dispatch, failure, and cancellation with console exporters.
- `samples/src/TinyDispatcher.Samples.Telemetry.AspNetCore` demonstrates operations beneath HTTP request activities and a dispatch initiated by a hosted worker.

See the [ASP.NET Core telemetry sample](../samples/src/TinyDispatcher.Samples.Telemetry.AspNetCore/README.md) for runnable PowerShell commands.
