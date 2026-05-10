# Multi-Context Afternoon TODO

Current checkpoint:

- Composition now coordinates two explicit use cases:
  - `AssemblyContributionComposition`: what this assembly contributes outward.
  - `HostGenerationComposition`: what the host generates from composed context inputs.
- Generation now has separate flows:
  - `EmitAssemblyContributionSources`
  - `EmitHostGenerationSources`
- `ContextGenerationInput` is now the composed context input only.
- Validation keeps the this-assembly pipeline separately through `ContextValidationInput`.

Next cleanup candidates:

1. Review whether `GeneratorContextComposition` is still the right top-level name.
   - It now coordinates more than contexts.
   - Possible names: `GeneratorComposition`, `GeneratorCompositionResult`, or `GeneratorUseCases`.

2. Consider splitting generation into two small services.
   - `AssemblyContributionGenerationPhase`
   - `HostGenerationPhase`
   - Keep `GeneratorGenerationPhase` as the coordinator, or remove it if it becomes thin enough.

3. Consider splitting composition internals by use case.
   - `BuildAssemblyContribution`
   - `BuildHostGeneration`
   - `BuildValidationContexts`
   - The current `BuildContext` still creates generation and validation inputs together.

4. Revisit model names after the split settles.
   - `ContextGenerationInput` might become `HostContextGenerationInput`.
   - `ContextValidationInput` might become `HostContextValidationInput`.
   - Only rename if it improves readability at call sites.

5. Add focused tests for the architectural split if behavior starts moving.
   - This slice is covered by existing tests.
   - If services split further, add tests around composition shape rather than only generated output.

Useful verification commands:

```powershell
dotnet test --no-restore
git diff --check
rg -n "LocalDiscovery|ComposedDiscovery|LocalPipeline|ComposedPipeline|HostCompositionInput|ThisAssemblyContributionInput" src tests
```
