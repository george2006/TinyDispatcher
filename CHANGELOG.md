## 1.0.4 - 2026-02-27

### Fixed
- Generated pipelines are now fully re-entrant and safe for concurrent dispatch within the same DI scope.
  Mutable execution state (`_index`, handler reference) has been moved from the scoped pipeline instance
  to a per-dispatch runtime object, eliminating potential race conditions when dispatching the same
  command type concurrently.

### Internal
- Refactored generated pipeline structure to isolate execution state per invocation.
- Added concurrency test coverage to validate thread-safety guarantees.
>>>>>>> fd80e007fe537793ad25329802f7b0cc7dc55f96
## 1.0.1

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
