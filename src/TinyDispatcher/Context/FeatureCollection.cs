using System;
using System.Collections.Generic;
using TinyDispatcher.Context;

namespace TinyDispatcher;

public sealed class FeatureCollection : IFeatureCollection
{
    private readonly Dictionary<Type, object?> _features = new();

    public object? this[Type featureType]
    {
        get
        {
            if (featureType is null) throw new ArgumentNullException(nameof(featureType));
            _features.TryGetValue(featureType, out var value);
            return value;
        }
        set
        {
            if (featureType is null) throw new ArgumentNullException(nameof(featureType));
            _features[featureType] = value;
        }
    }

    public TFeature? Get<TFeature>() where TFeature : class
        => this[typeof(TFeature)] as TFeature;

    public void Set<TFeature>(TFeature? instance) where TFeature : class
        => this[typeof(TFeature)] = instance;
}
