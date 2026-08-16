# TinyDispatcher OpenTelemetry ASP.NET Core Sample

Run the host:

```powershell
dotnet run --project samples/src/TinyDispatcher.Samples.Telemetry.AspNetCore
```

The self-hosted worker runs one reconciliation cycle after startup. In another terminal, send HTTP operations:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5099/orders -ContentType application/json -Body '{"orderId":"ORD-48291","customerEmail":"private@example.com"}'
Invoke-RestMethod -Uri http://localhost:5099/orders/ORD-48291
try { Invoke-RestMethod -Method Post -Uri http://localhost:5099/payments/fail } catch { $_.ErrorDetails.Message }
Invoke-RestMethod -Method Post -Uri http://localhost:5099/orders/cancel
```

Stop the host with `Ctrl+C`. The console exporter prints traces as operations complete and flushes aggregated metrics during shutdown.

The order ID and customer email are application payload values and are not emitted automatically.
