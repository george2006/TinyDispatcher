## 1.1.0 - 2026-05-18

### Added
- Multi-assembly composition support for TinyDispatcher.
  - Contributing assemblies publish structured assembly contributions.
  - Host projects compose final pipelines for commands contributed by referenced assemblies.
  - Referenced assemblies can contribute handler metadata, per-command middleware, policies, and context information.
  - A multi-project sample demonstrates cross-assembly dispatch and host-owned pipeline composition.
- Enhanced source generator diagnostics for referenced assembly composition, including provenance-aware conflict reporting.

### Changed
- TinyDispatcher now uses assembly-level contribution attributes as the compile-time transport for cross-assembly generator composition.
- Generated `ModuleInitializer`s publish `ThisAssemblyContribution.Create()` instead of only applying local DI actions.
- `DispatcherPipelineBootstrap` stores structured contribution snapshots while preserving existing DI application behavior.
- Non-host projects skip redundant referenced contribution validation, improving build performance.

### Fixed
- Referenced assemblies can now contribute global middleware to host-generated pipelines.
- Generator validation now uses the host's complete view of referenced assembly contributions.

### Notes
- The host remains the sole final composer.
- No reflection-based assembly scanning or runtime pipeline fallback was introduced.

## 1.1.0-rc3 - 2026-05-08

### Added
- **Enhanced source generator diagnostics** for multi-assembly composition scenarios.
  - Provenance-aware error reporting: diagnostics now indicate which referenced assembly caused a conflict.
  - Conflict detection for referenced contributions: identifies duplicate handler registrations and conflicting middleware contributions across assemblies.
  - Improved validation context to capture referenced contribution snapshots before validation.

### Fixed
- Generator now properly validates referenced contributions with the host's complete view of the assembly graph.
- Non-host projects skip redundant referenced contribution validation, improving build performance.

### Internal
- Expanded test coverage for referenced contribution diagnostics and conflict scenarios.
- Refactored generator validation to isolate referenced contribution handling.

### Notes
- This is the final release candidate before general availability (GA).
- The hardening focus strengthens compile-time guarantees for multi-assembly scenarios.

## 1.1.0-rc2 - 2026-04-30

### Fixed
- Referenced assemblies can now contribute global middleware to host-generated pipelines.
- The multi-project sample now covers a fuller vertical slice with referenced handlers, global middleware, and policies.

## 1.1.0-rc1 - 2026-04-23

### Added
- Multi-assembly composition support for TinyDispatcher.
  - Contributing assemblies now publish structured assembly contributions.
  - The host generator composes final pipelines for commands contributed by referenced assemblies.
  - Referenced assemblies can contribute handler metadata, per-command middleware, policies, and context information.
  - A new multi-project sample demonstrates cross-assembly dispatch and host-owned pipeline composition.

### Changed
- TinyDispatcher now uses assembly-level contribution attributes as the compile-time transport for cross-assembly generator composition.
- Generated `ModuleInitializer`s publish `ThisAssemblyContribution.Create()` instead of only applying local DI actions.
- `DispatcherPipelineBootstrap` stores structured contribution snapshots while preserving existing DI application behavior.

### Notes
- The host remains the sole final composer.
- No reflection-based assembly scanning or runtime pipeline fallback was introduced.

## 1.0.4 - 2026-02-27

### Fixed
- Generated pipelines are now fully re-entrant and safe for concurrent dispatch within the same DI scope.
  Mutable execution state (`_index`, handler reference) has been moved from the scoped pipeline instance
  to a per-dispatch runtime object, eliminating potential race conditions when dispatching the same
  command type concurrently.

### Internal
- Refactored generated pipeline structure to isolate execution state per invocation.
- Added concurrency test coverage to validate thread-safety guarantees.

## 1.0.1

### Fixed
- Fixed pipeline map emission regression (generator now emits maps when enabled).
- Added regression tests for pipeline map emission.
- Fixed nullable warning in `PipelineMapFormats.Parse`.

### Added
- **Pipeline Maps**: explicit, inspectable execution paths for commands.
  - The full middleware + handler execution chain is now deterministic and materialized.
  - Pipelines are generated at compile time via source generators.
  - No reflection or runtime guessing on the hot path.

### Changed
- Removed runtime pipeline selection.
- Pipelines are now resolved directly via DI per command.

### Performance
- Reduced allocations significantly in middleware execution.
- Improved dispatch throughput in 5-middleware benchmark scenarios.

### Breaking Changes
- Middleware registration model updated.
