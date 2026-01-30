## [Unreleased]

### Added
- **Pipeline Maps**: explicit, inspectable execution paths for commands.
  - The full middleware + handler execution chain is now deterministic and materialized.
  - Pipelines are generated at compile time via source generators.
  - No reflection or runtime guessing on the hot path.
