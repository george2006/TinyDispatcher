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

## Generator

The generator:

- finds handlers at compile time
- builds a pipeline plan
- emits pipelines + dispatcher code into a configurable namespace
