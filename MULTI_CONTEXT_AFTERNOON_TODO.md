# Multi-Context RC Review Notes

Current checkpoint:

- Extraction separates this-assembly facts from referenced assembly facts.
- Composition coordinates explicit use cases:
  - `ThisAssemblyContributionDiscovery`: local contribution discovery for this assembly.
  - `HostGenerationComposition`: host-effective generation facts.
  - `HostContextValidationInput`: validation facts derived from host projections.
- Host composition owns host context projection and referenced contribution merging.
- Generation is split by use case:
  - `AssemblyContributionGenerationPhase`
  - `HostGenerationPhase`
- Low-level emitters and writers are reused unchanged.

RC verification:

1. Source-generation multi-context compile coverage is present in `MultiContextGenerationTests`.
2. Private nested handlers are ignored so generated contribution metadata does not emit inaccessible `typeof(...)` references.
3. Public docs now describe multiple host contexts.
4. Full test suite and whitespace checks are passing.

Remaining follow-up:

1. Consider a dedicated runtime integration project that references the source generator as an analyzer.
2. Keep stale single-context/composed naming searches clean before merge.

```powershell
dotnet test --no-restore
git diff --check
```

Final review question:

> Does each phase read as one responsibility, or is it still carrying another phase's decision?
