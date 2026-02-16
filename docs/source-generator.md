# Source generator

TinyDispatcher is finalized at build time by a source generator.

This generator performs:

- compile-time discovery of handlers
- pipeline planning (layering and selection)
- generation of dispatcher and pipeline types
- optional pipeline map emission

## Assembly-level configuration (recommended)

Configure the generator with an assembly attribute:

```csharp
using TinyDispatcher;

[assembly: TinyDispatcherGeneratorOptions(
    GeneratedNamespace = "MyApp.Dispatcher.Generated",
    IncludeNamespacePrefix = "MyApp.",
    EmitPipelineMap = true,
    PipelineMapFormat = "json"
)]
```

## Notes

- Prefer assembly-level config over MSBuild properties when possible.
- Keep `GeneratedNamespace` stable to avoid churn in generated type names across projects.
