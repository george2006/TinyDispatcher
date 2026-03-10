using TinyDispatcher;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.UnitTests.Context;

public sealed class FeatureCollectionExtensionsTests
{
    private sealed class TestFeature
    {
    }

    [Fact]
    public void Add_registers_feature_and_returns_same_collection()
    {
        IFeatureCollection features = new FeatureCollection();
        var feature = new TestFeature();

        var result = features.Add(feature);

        Assert.Same(features, result);
        Assert.Same(feature, features.Get<TestFeature>());
    }
}