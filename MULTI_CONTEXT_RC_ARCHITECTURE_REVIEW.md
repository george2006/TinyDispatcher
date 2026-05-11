# Multi-Lane RC Architecture Review

Date: 2026-05-10

## Current read

The generation path is now easy to follow from a fresh-entry perspective:

1. `GeneratorGenerationPhase` coordinates the generation use cases.
2. `HostGenerationPhase` plans and emits host-owned pipeline sources and pipeline maps.
3. `AssemblyContributionGenerationPhase` emits this assembly's contribution surface, handler registrations, and module initializer.
4. Low-level emitters and writers remain reused.

The feature now reads as a product shape rather than an experimental branch:

- bounded contexts are explicit
- each host context gets its own generation plan
- assembly contribution and host generation are separate use cases
- no-op, shipped `AppContext`, and custom contexts compose together
- runtime remains simple while source generation owns composition

## What looks good

- `GeneratorGenerationPhase` is a small top-level coordinator.
- `HostGenerationPhase` is mostly about host source generation.
- `AssemblyContributionGenerationPhase` owns publication and contribution artifacts.
- Composition now names domain objects directly:
  - `ThisAssemblyExtraction`
  - `HostGenerationComposition`
  - `HostContextProjection`
  - `HostContextGenerationInput`
  - `HostContextValidationInput`
- The context-lane RC sample demonstrates vertical-slice usage clearly:
  - `Orders`
  - `Payments`
  - `DefaultAppContext`
  - `NoOp`
  - `Shared`

## Review notes for tomorrow

### 1. Assembly contribution still depends on host generation output

`AssemblyContributionGenerationPhase.Generate(...)` takes both:

- `AssemblyContributionSourcePlan`
- `HostGenerationSourcePlan`

This is currently correct because `ThisAssemblyContribution` needs to call generated host pipeline registration methods. Still, a fresh reader may ask why assembly contribution generation knows about host generation.

Possible cleanup:

- introduce a small `HostPipelinePublicationPlan`
- pass only the publication facts needed by assembly contribution emission
- keep `HostGenerationSourcePlan` focused on host source emission

### 2. `EmptyPipelineContributionEmitter` has outgrown its name

The emitter no longer only handles an empty pipeline contribution. It emits the broader `ThisAssemblyContribution` surface:

- contribution metadata
- handler bindings
- pipeline bindings
- policy bindings
- generated pipeline registration hooks

Possible rename:

- `AssemblyContributionEmitter`
- `ThisAssemblyContributionEmitter`

### 3. `HostGenerationSourcePlan` may carry publication facts

`HostGenerationSourcePlan` currently contains top-level `Discovery` and `EmitOptions`. Those fields are useful for module initializer/publication decisions, but the host generation emitter mostly works through context plans.

Question for tomorrow:

> Are these fields host generation facts, or publication facts?

If they are publication facts, move them into the suggested publication plan.

### 4. Emit option naming could become clearer

`HostGenerationPhase` has both:

- `BuildEmitOptions`
- `BuildContextEmitOptions`

The behavior is understandable, but the naming is subtle.

Possible names:

- `BuildAssemblyWideEmitOptions`
- `BuildContextScopedEmitOptions`

### 5. Keep an eye on global namespace support

The new sample exposed that middleware naming did not strip `global::` before creating constructor parameter names. This was fixed in `PipelineNameFactory`.

Tomorrow review question:

> Are there any other places where FQNs are reused as identifiers without normalization?

## Verification completed

Commands run:

```powershell
dotnet build samples\src\TinyDispatcher.Samples.MultiContextRc\TinyDispatcher.Samples.MultiContextRc.csproj --no-restore
dotnet run --project samples\src\TinyDispatcher.Samples.MultiContextRc\TinyDispatcher.Samples.MultiContextRc.csproj --no-build
dotnet test --no-restore
git diff --check
```

Results:

- sample build passed
- sample run passed
- full test suite passed: 212 tests
- whitespace check passed

## Final RC feeling

The architecture is understandable and close to RC quality. The remaining concerns are mostly naming and responsibility polish, not broken design.
