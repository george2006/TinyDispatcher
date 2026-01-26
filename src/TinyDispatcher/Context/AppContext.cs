using System;
using TinyDispatcher.Context;

namespace TinyDispatcher;

public sealed class AppContext : IAppContext
{
    private readonly IFeatureCollection _features;

    public AppContext(IFeatureCollection? features = null)
        => _features = features ?? new FeatureCollection();

    public TFeature GetFeature<TFeature>() where TFeature : class
        => _features.Get<TFeature>()
           ?? throw new InvalidOperationException(
               $"TinyDispatcher: feature '{typeof(TFeature).FullName}' is not registered in the current context.");

    public bool TryGetFeature<TFeature>(out TFeature feature) where TFeature : class
    {
        feature = _features.Get<TFeature>()!;
        return feature is not null;
    }

    public TFeature? GetFeatureOrDefault<TFeature>() where TFeature : class
        => _features.Get<TFeature>();
}
