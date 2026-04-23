# Architecture overview

TinyDispatcher consists of:

- a small runtime core (interfaces + dispatcher + DI glue)
- a source generator that plans and emits code
- samples demonstrating usage patterns

## Runtime core

The runtime contains:

- ICommand / IQuery abstractions
- handler interfaces
- dispatcher
- pipeline runtime interfaces
- DI extension methods

At startup, generated code publishes contributions via a small bootstrap hook:

- the generator emits a `ModuleInitializer` per project
- module initializers publish structured assembly contributions
- `UseTinyDispatcher<TContext>` calls `DispatcherPipelineBootstrap.Apply(services)` to apply generated registrations and contribution metadata

## Generator

The generator:

- finds handlers at compile time
- discovers local pipeline facts
- consumes referenced assembly contribution metadata
- builds the final host pipeline plan
- emits pipelines + dispatcher code into a configurable namespace

The generator also emits diagnostics for invalid shapes/config (duplicate handlers, invalid middleware shapes, missing host call, etc.).

## Ownership boundaries

- Contributing assemblies analyze local handlers, middleware, and policies at build time.
- Contributing assemblies publish structured metadata through generated code and assembly attributes.
- The host owns final pipeline composition.
- The runtime applies generated registrations and stores contribution snapshots, but does not perform discovery or runtime scanning.

For the multi-assembly flow, see [Multi-Assembly Composition](multi-assembly-composition.md).
