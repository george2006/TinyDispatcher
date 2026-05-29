#nullable enable

namespace TinyDispatcher.SourceGen.Generator.Models;

/// <summary>
/// Represents an open generic middleware type discovered from bootstrap or policy configuration.
/// Invariant:
///   - OpenTypeFqn is the base fully-qualified name WITHOUT generic arguments
///   - Arity matches the open generic type arity
/// </summary>
public readonly record struct MiddlewareRef(
    string OpenTypeFqn,
    int Arity);
