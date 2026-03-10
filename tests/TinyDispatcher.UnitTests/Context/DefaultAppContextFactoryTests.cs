using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.UnitTests.Context;

public sealed class DefaultAppContextFactoryTests
{
    private sealed class TestFeature
    {
        public string Value { get; }

        public TestFeature(string value)
        {
            Value = value;
        }
    }

    private sealed class TestFeatureInitializer : IFeatureInitializer
    {
        private readonly TestFeature _feature;

        public bool WasCalled { get; private set; }

        public TestFeatureInitializer(TestFeature feature)
        {
            _feature = feature;
        }

        public void Initialize(IFeatureCollection features)
        {
            WasCalled = true;
            features.Set(_feature);
        }
    }

    [Fact]
    public async Task Create_async_when_no_initializers_returns_context()
    {
        var factory = new DefaultAppContextFactory();

        var context = await factory.CreateAsync();

        Assert.NotNull(context);
    }

    [Fact]
    public async Task Create_async_when_no_initializers_creates_empty_context()
    {
        var factory = new DefaultAppContextFactory();

        var context = await factory.CreateAsync();

        var feature = context.GetFeatureOrDefault<TestFeature>();

        Assert.Null(feature);
    }

    [Fact]
    public async Task Create_async_executes_initializer()
    {
        var feature = new TestFeature("hello");
        var initializer = new TestFeatureInitializer(feature);

        var factory = new DefaultAppContextFactory(new[] { initializer });

        var context = await factory.CreateAsync();

        Assert.True(initializer.WasCalled);
        Assert.Same(feature, context.GetFeature<TestFeature>());
    }

    [Fact]
    public async Task Create_async_executes_multiple_initializers()
    {
        var f1 = new TestFeature("one");
        var f2 = new TestFeature("two");

        var i1 = new TestFeatureInitializer(f1);
        var i2 = new TestFeatureInitializer(f2);

        var factory = new DefaultAppContextFactory(new IFeatureInitializer[]
        {
            i1,
            i2
        });

        var context = await factory.CreateAsync();

        Assert.True(i1.WasCalled);
        Assert.True(i2.WasCalled);

        var feature = context.GetFeature<TestFeature>();

        Assert.NotNull(feature);
    }

    [Fact]
    public async Task Create_async_initializer_can_register_feature()
    {
        var feature = new TestFeature("registered");
        var initializer = new TestFeatureInitializer(feature);

        var factory = new DefaultAppContextFactory(new[] { initializer });

        var context = await factory.CreateAsync();

        var result = context.GetFeature<TestFeature>();

        Assert.Same(feature, result);
    }
}