## [1.0.4] - 2026-02-26

### Added
- UseTinyNoOpContext() for zero-context dispatch.

### Generator
- Context inference updated to support NoOpContext.

### Internal
- Documentation updates.
- Minor validation polish.

### Breaking Changes
- None

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
