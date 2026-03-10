using System;
using TinyDispatcher;
using Xunit;

namespace TinyDispatcher.UnitTests.Context;

public sealed class FeatureCollectionTests
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
    public void Indexer_get_when_feature_type_is_null_throws_argument_null_exception()
    {
        var features = new FeatureCollection();

        var exception = Assert.Throws<ArgumentNullException>(() => _ = features[(Type)null!]);

        Assert.Equal("featureType", exception.ParamName);
    }

    [Fact]
    public void Indexer_set_when_feature_type_is_null_throws_argument_null_exception()
    {
        var features = new FeatureCollection();

        var exception = Assert.Throws<ArgumentNullException>(() => features[(Type)null!] = new object());

        Assert.Equal("featureType", exception.ParamName);
    }

    [Fact]
    public void Indexer_get_when_feature_is_missing_returns_null()
    {
        var features = new FeatureCollection();

        var result = features[typeof(TestFeature)];

        Assert.Null(result);
    }

    [Fact]
    public void Indexer_set_when_feature_is_registered_returns_same_instance()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("hello");

        features[typeof(TestFeature)] = expected;

        var result = features[typeof(TestFeature)];

        Assert.Same(expected, result);
    }

    [Fact]
    public void Get_when_feature_is_registered_returns_feature()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("hello");
        features[typeof(TestFeature)] = expected;

        var result = features.Get<TestFeature>();

        Assert.Same(expected, result);
    }

    [Fact]
    public void Get_when_feature_is_missing_returns_null()
    {
        var features = new FeatureCollection();

        var result = features.Get<TestFeature>();

        Assert.Null(result);
    }

    [Fact]
    public void Set_when_feature_is_registered_stores_feature_by_type()
    {
        var features = new FeatureCollection();
        var expected = new TestFeature("hello");

        features.Set(expected);

        var result = features[typeof(TestFeature)];

        Assert.Same(expected, result);
    }

    [Fact]
    public void Set_when_instance_is_null_stores_null_value()
    {
        var features = new FeatureCollection();

        features.Set<TestFeature>(null);

        var result = features.Get<TestFeature>();

        Assert.Null(result);
    }

    [Fact]
    public void Set_when_feature_type_is_already_registered_overwrites_existing_value()
    {
        var features = new FeatureCollection();
        var first = new TestFeature("first");
        var second = new TestFeature("second");

        features.Set(first);
        features.Set(second);

        var result = features.Get<TestFeature>();

        Assert.Same(second, result);
    }

    [Fact]
    public void Indexer_set_when_value_is_null_clears_registered_feature()
    {
        var features = new FeatureCollection();
        var feature = new TestFeature("hello");

        features.Set(feature);
        features[typeof(TestFeature)] = null;

        var result = features.Get<TestFeature>();

        Assert.Null(result);
    }
}