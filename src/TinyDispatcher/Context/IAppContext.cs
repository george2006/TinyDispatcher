namespace TinyDispatcher.Context;

public interface IAppContext
{
    TFeature GetFeature<TFeature>() where TFeature : class;
    bool TryGetFeature<TFeature>(out TFeature feature) where TFeature : class;
    TFeature? GetFeatureOrDefault<TFeature>() where TFeature : class;
}
