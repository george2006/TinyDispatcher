#nullable enable

using TinyDispatcher.Context;

namespace TinyDispatcher;

public static class FeatureCollectionExtensions
{
    public static IFeatureCollection Add<TFeature>(this IFeatureCollection features, TFeature instance)
        where TFeature : class
    {
        features.Set<TFeature>(instance);
        return features;
    }
}
