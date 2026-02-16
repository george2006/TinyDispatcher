# Design decisions

This is a living document capturing why TinyDispatcher is built the way it is.

## Not a full framework

TinyDispatcher is intentionally not a full CQRS framework. It is a focused building block.

## Compile-time first

- Handler discovery at build time
- Pipeline composition at build time
- Readable generated code

## Deterministic layering

Middleware order is deterministic and enforced:

**Global → Policy → Per-command → Handler**

## Debuggability

Generated pipelines and dispatcher code are meant to be read and debugged like hand-written code.
