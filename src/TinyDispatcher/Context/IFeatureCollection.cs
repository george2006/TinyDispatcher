using System;

namespace TinyDispatcher.Context;

public interface IFeatureCollection
{
    object? this[Type featureType] { get; set; }

    TFeature? Get<TFeature>() where TFeature : class;
    void Set<TFeature>(TFeature? instance) where TFeature : class;
}
