# Source generator

TinyDispatcher is finalized at build time by a source generator.

This generator performs:

- compile-time discovery of handlers
- extraction of referenced assembly contribution metadata
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

### Bootstrap detection (UseTinyDispatcher vs UseTinyNoOpContext)

The generator discovers your bootstrap call from your composition root. Exactly one bootstrap call is expected:

- `UseTinyDispatcher<TContext>(...)`
- `UseTinyNoOpContext(...)` (uses `NoOpContext`)

If `UseTinyNoOpContext` is used, the generator can emit a pipeline that avoids context-related work.

## Multi-assembly contribution flow

In a multi-project setup:

- non-host assemblies analyze their own handlers and pipeline-relevant facts
- they emit generated assembly contributions for runtime publication
- they also emit assembly-level contribution attributes as compile-time metadata
- the host generator reads those referenced contributions and composes final pipelines

Important rules:

- discovery happens in the source generator, not in the module initializer
- module initializers are publication hooks only
- the host remains the final composer
- no reflection-based assembly scanning is used

See [Multi-Assembly Composition](multi-assembly-composition.md) for the full model.
