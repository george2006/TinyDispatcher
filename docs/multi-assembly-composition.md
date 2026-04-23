# Multi-Assembly Composition

TinyDispatcher supports a modular composition model where handlers, middleware, and policies can live in referenced class libraries while the host still owns final pipeline composition.

## Design goals

- keep discovery at compile time
- keep the runtime simple
- avoid reflection-based assembly scanning
- keep one final composition path
- let the host remain the sole final composer

## High-level model

There are two roles:

- **Contributing assembly**
- **Host assembly**

### Contributing assemblies

A contributing assembly:

- discovers its local handlers at build time
- discovers local pipeline-relevant facts such as:
  - command handlers
  - per-command middleware bindings
  - policy bindings
  - context information
- emits generated runtime publication code
- emits assembly-level contribution attributes for referenced host generators to consume

It does **not** generate the final dispatcher graph for the whole app.

### Host assembly

The host assembly:

- defines the final bootstrap through `UseTinyDispatcher<TContext>(...)`
- owns global middleware configuration
- consumes referenced assembly contribution metadata
- composes the full command universe
- emits and registers final pipelines for local and referenced commands

## Publication and transport

TinyDispatcher uses two related transports:

### Runtime transport

Each contributing assembly emits `ThisAssemblyContribution` with:

- `Create()`
- `AddServices(IServiceCollection)`

The generated `ModuleInitializer` publishes `ThisAssemblyContribution.Create()` through `DispatcherPipelineBootstrap.AddContribution(...)`.

This runtime transport is used to:

- apply generated registrations
- expose structured contribution snapshots for future runtime validation/composition work

### Compile-time transport

Each contributing assembly also emits assembly-level contribution attributes.

These attributes carry compile-time facts such as:

- handler bindings
- context type
- per-command middleware bindings
- policy bindings

The host generator reads those attributes from referenced assemblies during source generation.

## Important rules

- Discovery happens in the source generator.
- Module initializers are publication hooks only.
- The host remains the final composer.
- No reflection-based assembly scanning is used.
- Runtime does not reconstruct pipeline behavior dynamically.

## Pipeline precedence

Pipeline precedence remains:

**Per-command -> Policy -> Global**

This rule applies the same way for:

- host-local commands
- commands contributed from referenced assemblies

## Current supported scope

The current multi-assembly composition path supports:

- command handlers
- context metadata
- per-command middleware
- policy metadata
- host-owned global middleware

## Mental model

The simplest way to think about it is:

- libraries publish facts
- host composes
- runtime applies

That keeps TinyDispatcher aligned with its compile-time-first design while enabling real modular monolith and multi-project composition.
