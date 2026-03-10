using System;
using TinyDispatcher;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.UnitTests.Context;

public sealed class AppContextTests
{
    private sealed class TestFeature
    {
        public string Value { get; }

        public TestFeature(string value)
        {
            Value = value;
        }
    }

    [Fact]
    public void Constructor_when_no_features_provided_uses_empty_feature_collection()
    {
        var context = new AppContext();

        var result = context.GetFeatureOrDefault<TestFeature>();

        Assert.Null(result);
    }

    [Fact]
    public void Get_feature_when_feature_is_registered_returns_feature()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("hello");
        features.Set(expected);

        var context = new AppContext(features);

        var result = context.GetFeature<TestFeature>();

        Assert.Same(expected, result);
    }

    [Fact]
    public void Get_feature_when_feature_is_missing_throws_invalid_operation_exception()
    {
        var context = new AppContext();

        var exception = Assert.Throws<InvalidOperationException>(() => context.GetFeature<TestFeature>());

        Assert.Equal(
            $"TinyDispatcher: feature '{typeof(TestFeature).FullName}' is not registered in the current context.",
            exception.Message);
    }

    [Fact]
    public void Try_get_feature_when_feature_exists_returns_true_and_feature()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("value");
        features.Set(expected);

        var context = new AppContext(features);

        var result = context.TryGetFeature<TestFeature>(out var feature);

        Assert.True(result);
        Assert.Same(expected, feature);
    }

    [Fact]
    public void Try_get_feature_when_feature_is_missing_returns_false_and_null()
    {
        var context = new AppContext();

        var result = context.TryGetFeature<TestFeature>(out var feature);

        Assert.False(result);
        Assert.Null(feature);
    }

    [Fact]
    public void Get_feature_or_default_when_feature_exists_returns_feature()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("abc");
        features.Set(expected);

        var context = new AppContext(features);

        var result = context.GetFeatureOrDefault<TestFeature>();

        Assert.Same(expected, result);
    }

    [Fact]
    public void Get_feature_or_default_when_feature_is_missing_returns_null()
    {
        var context = new AppContext();

        var result = context.GetFeatureOrDefault<TestFeature>();

        Assert.Null(result);
    }
}