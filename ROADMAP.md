# TinyDispatcher Roadmap

This document outlines the current direction of the project.
It is intentionally short and focused.

TinyDispatcher prioritizes architectural clarity, performance, and predictability over feature expansion.

---

## Current Phase

Focus: stable `1.1.x` support and `1.2.0-rc*` feedback.

- Keep `1.1.x` stable for production use and hotfixes.
- Use `stable/1.1.x` for patch releases.
- Publish multi-context / context-lane work as `1.2.0-rc*`.
- Treat context lanes as release-candidate API until final `1.2.0`.
- Keep expanding diagnostics, tests, samples, and documentation around modular composition.

---

## Short-Term Goals

- Publish the first `1.2.0-rc*` package for multi-context / context-lane final feedback.
- Document stable `1.1.x` versus release candidate `1.2.0-rc*` everywhere multi-lane behavior appears.
- Keep improving generator diagnostics (clear compile-time errors).
- Keep improving middleware shape validation.
- Expand integration samples (Azure Functions, Worker Service).
- Benchmark suite documentation refresh

---

## Long-Term Direction

TinyDispatcher aims to remain:

- Minimal
- Predictable
- Allocation-conscious
- Explicit over magical


The goal is to stay small, sharp, and intentional.
