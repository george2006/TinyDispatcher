#nullable enable

using System;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Models;

/// <summary>
/// Represents an open generic middleware type discovered from bootstrap or policy configuration.
/// Invariant:
///   - OpenTypeFqn is the base fully-qualified name WITHOUT generic arguments
///   - Arity matches the open generic type arity
///   - MiddlewareLocation is diagnostic metadata only; it does not participate in equality, so
///     dedup (see MiddlewareOrdering, PolicySpecBuilder) still treats the same middleware type
///     declared at different call sites as one entry.
/// </summary>
public readonly record struct MiddlewareRef(
    string OpenTypeFqn,
    int Arity)
{
    public Location MiddlewareLocation { get; init; } = Location.None;

    public MiddlewareRef(string openTypeFqn, int arity, Location location)
        : this(openTypeFqn, arity)
    {
        MiddlewareLocation = location;
    }

    public bool Equals(MiddlewareRef other)
        => string.Equals(OpenTypeFqn, other.OpenTypeFqn, StringComparison.Ordinal)
           && Arity == other.Arity;

    public override int GetHashCode()
    {
        unchecked
        {
            return (StringComparer.Ordinal.GetHashCode(OpenTypeFqn) * 397) ^ Arity;
        }
    }
}
