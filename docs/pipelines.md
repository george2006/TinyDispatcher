# Pipelines and layering

TinyDispatcher composes middleware pipelines at build time and emits readable code.

In a multi-assembly setup, the host composes these pipelines from:

- local bootstrap configuration
- locally discovered handlers
- structured contributions published by referenced assemblies

## Layering order

The runtime pipeline for a command is deterministic:

**Global -> Policy -> Per-command -> Handler**

This order is enforced by the planner and reflected in the generated pipeline switch.

## Pipeline kinds

TinyDispatcher emits pipelines in these forms:

- **Global pipeline**: open-generic pipeline used for commands without policy/per-command pipelines
- **Policy pipeline**: open-generic pipeline generated per policy and reused across commands
- **Per-command pipeline**: closed pipeline generated for a specific command (includes all applicable layers)

These same pipeline kinds can now be composed for commands contributed by referenced assemblies, as long as the host sees the corresponding contribution metadata.

## Policies

Policies are a way to apply middleware to a group of commands.
A policy pipeline is typically open-generic and is registered for commands that match the policy.

(Your policies configuration lives in the TinyDispatcher builder you pass to `UseTinyDispatcher<TContext>()`.)

In multi-assembly composition:

- host-configured global middleware still applies host-wide
- referenced assemblies may contribute per-command middleware and policy metadata
- the host generator resolves the final precedence using the same rules as single-assembly composition

## Readability

Generated pipelines are intended to be debug-friendly:

- constructor-injected middleware
- switch-based execution (no delegate chains/closures in the hot path)
- explicit runtime `NextAsync` progression
