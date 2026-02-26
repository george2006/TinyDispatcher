#nullable enable

namespace TinyDispatcher.Context;

/// <summary>
/// A context that carries nothing. Use this for maximum throughput when you don't need context.
/// </summary>
public readonly struct NoOpContext
{
    public static readonly NoOpContext Instance = default;
}