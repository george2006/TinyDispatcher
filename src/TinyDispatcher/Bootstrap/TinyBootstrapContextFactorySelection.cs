using System;

namespace TinyDispatcher.Bootstrap;

internal enum TinyBootstrapContextFactoryKind
{
    None,
    FactoryType,
    DefaultFactory
}

internal sealed class TinyBootstrapContextFactorySelection
{
    private TinyBootstrapContextFactorySelection(
        TinyBootstrapContextFactoryKind kind,
        Type? factoryType)
    {
        Kind = kind;
        FactoryType = factoryType;
    }

    public TinyBootstrapContextFactoryKind Kind { get; }
    public Type? FactoryType { get; }

    public static TinyBootstrapContextFactorySelection None { get; } =
        new(TinyBootstrapContextFactoryKind.None, factoryType: null);

    public static TinyBootstrapContextFactorySelection DefaultFactory { get; } =
        new(TinyBootstrapContextFactoryKind.DefaultFactory, factoryType: null);

    public static TinyBootstrapContextFactorySelection FromFactoryType(Type factoryType)
        => new(TinyBootstrapContextFactoryKind.FactoryType, factoryType);
}
