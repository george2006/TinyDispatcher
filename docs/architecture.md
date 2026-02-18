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

At startup, generated code registers pipelines via a small bootstrap hook:

- the generator emits a `ModuleInitializer` per project
- module initializers contribute pipeline registrations
- `UseTinyDispatcher<TContext>` calls `DispatcherPipelineBootstrap.Apply(services)` to apply them

## Generator

The generator:

- finds handlers at compile time
- builds a pipeline plan
- emits pipelines + dispatcher code into a configurable namespace

The generator also emits diagnostics for invalid shapes/config (duplicate handlers, invalid middleware shapes, missing host call, etc.).
