#nullable enable

using Microsoft.Extensions.DependencyInjection;
using System;
using TinyDispatcher.Bootstrap;
using TinyDispatcher.Context;
using Xunit;

namespace TinyDispatcher.UnitTests.Bootstrap;

public sealed class TinyBootstrapTests
{
    [Fact]
    public void Throws_when_services_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => new TinyBootstrap(null!));
    }

    [Fact]
    public void Returns_same_instance_when_using_global_middleware()
    {
        var services = CreateServices();
        var sut = CreateSut(services);

        var result = sut.UseGlobalMiddleware(typeof(DummyOpenGenericMiddleware<>));

        Assert.Same(sut, result);
    }

    [Fact]
    public void Returns_same_instance_when_using_middleware_for_generic_command()
    {
        var services = CreateServices();
        var sut = CreateSut(services);

        var result = sut.UseMiddlewareFor<TestCommand>(typeof(DummyOpenGenericMiddleware<>));

        Assert.Same(sut, result);
    }

    [Fact]
    public void Returns_same_instance_when_using_middleware_for_command_type()
    {
        var services = CreateServices();
        var sut = CreateSut(services);

        var result = sut.UseMiddlewareFor(typeof(TestCommand), typeof(DummyOpenGenericMiddleware<>));

        Assert.Same(sut, result);
    }

    [Fact]
    public void Returns_same_instance_when_using_policy()
    {
        var services = CreateServices();
        var sut = CreateSut(services);

        var result = sut.UsePolicy<TestPolicy>();

        Assert.Same(sut, result);
    }

    [Fact]
    public void Adds_feature_initializer_registration()
    {
        var services = CreateServices();
        var sut = CreateSut(services);

        var result = sut.AddFeatureInitializer<TestFeatureInitializer>();

        Assert.Same(sut, result);
        AssertContainsScopedRegistration<IFeatureInitializer, TestFeatureInitializer>(services);
    }

    private static ServiceCollection CreateServices()
        => new();

    private static TinyBootstrap CreateSut(IServiceCollection services)
        => new(services);

    private static void AssertContainsScopedRegistration<TService, TImplementation>(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(TService) &&
                descriptor.ImplementationType == typeof(TImplementation) &&
                descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private sealed class TestCommand : ICommand;

    private sealed class DummyOpenGenericMiddleware<TCommand>;

    private sealed class TestPolicy;

    private sealed class TestFeatureInitializer : IFeatureInitializer
    {
        public void Initialize(IFeatureCollection features)
        {
            throw new NotImplementedException();
        }
    }
}