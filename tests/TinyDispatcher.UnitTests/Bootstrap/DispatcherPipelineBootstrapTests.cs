#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Bootstrap;
using Xunit;

namespace TinyDispatcher.UnitTests.Bootstrap;

public sealed class DispatcherPipelineBootstrapTests
{
    [Fact]
    public void Throws_when_services_is_null()
    {
        Assert.Throws<ArgumentNullException>(() => DispatcherPipelineBootstrap.Apply(null!));
    }

    [Fact]
    public void Ignores_null_contribution()
    {
        var services = CreateServices();

        DispatcherPipelineBootstrap.AddContribution(null!);

        var exception = Record.Exception(() => DispatcherPipelineBootstrap.Apply(services));

        Assert.Null(exception);
    }

    [Fact]
    public void Applies_registered_contribution()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(AddTestService);

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        AssertSingleRegistration<TestService>(services);
    }

    [Fact]
    public void Applies_contributions_only_once_per_service_collection()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(AddTestService);

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);

        AssertSingleRegistration<TestService>(services);
    }

    [Fact]
    public void Applies_stored_contributions_to_each_service_collection()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(AddTestService);

        var first = CreateServices();
        var second = CreateServices();

        DispatcherPipelineBootstrap.Apply(first);
        DispatcherPipelineBootstrap.Apply(second);

        AssertSingleRegistration<TestService>(first);
        AssertSingleRegistration<TestService>(second);
    }

    [Fact]
    public void Adds_bootstrap_marker_only_once_for_same_service_collection()
    {
        ResetStore();

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);

        Assert.Equal(1, CountBootstrapMarkers(services));
    }

    private static ServiceCollection CreateServices()
        => new();

    private static void AddTestService(IServiceCollection services)
        => services.AddSingleton<TestService>();

    private static void AssertSingleRegistration<TService>(IServiceCollection services)
    {
        var count = CountRegistrations<TService>(services);
        Assert.Equal(1, count);
    }

    private static int CountRegistrations<TService>(IServiceCollection services)
    {
        var count = 0;

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TService))
                count++;
        }

        return count;
    }

    private static int CountBootstrapMarkers(IServiceCollection services)
    {
        var count = 0;

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType.Name == "DispatcherPipelineBootstrapAppliedMarker")
                count++;
        }

        return count;
    }

    private static void ResetStore()
    => PipelineContributionStore.ResetForTests();

    private sealed class TestService;
}