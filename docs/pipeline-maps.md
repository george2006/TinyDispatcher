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

Pipeline maps support three output formats:

- `json` emits one readable JSON map per operation inside generated source comments;
- `mermaid` emits one Mermaid graph per operation;
- `attributes` emits one `TinyDispatcherPipelineMapAttribute` per selected generated pipeline.

Separate combined formats with `;`:

```csharp
[assembly: TinyDispatcherGeneratorOptions(
    EmitPipelineMap = true,
    PipelineMapFormat = "json;attributes"
)]
```

The attribute format groups every command using the same generated pipeline. It preserves
the ordered middleware list, the dispatcher context, the selected policy, and the global
and policy middleware boundaries. The remaining middleware entries belong to the
operation-specific portion of the pipeline.

Read the metadata through the public pipeline-map API:

```csharp
var maps = DispatcherPipelineMaps.Get();
```

Each `DispatcherPipelineMap` exposes pipeline, context, command, middleware, and policy
types across the loaded application modules without coupling consumers to the generated
attribute representation. Use `Get(assembly)` only when a tool deliberately needs to
inspect one assembly.

Pipeline-map attributes are a generated storage detail. They do not participate in
dispatch, dependency injection, or pipeline execution. TinyDispatcher only reads them
when `DispatcherPipelineMaps.Get` is called.
