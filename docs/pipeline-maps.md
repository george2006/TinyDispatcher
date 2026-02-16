# Pipeline maps

Pipeline maps provide compile-time introspection into what TinyDispatcher generated.

A pipeline map can help you answer:

- Which middleware runs for a command?
- Which pipeline type was selected (global/policy/per-command)?
- What is the final deterministic order?

## Enabling pipeline maps

Enable via generator options (assembly attribute):

```csharp
[assembly: TinyDispatcherGeneratorOptions(
    EmitPipelineMap = true,
    PipelineMapFormat = "json"
)]
```

## Formats

Supported formats depend on your generator options (commonly JSON). The output is emitted at build time alongside generated sources.
