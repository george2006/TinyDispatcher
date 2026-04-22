#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Bootstrap;
using Xunit;

namespace TinyDispatcher.UnitTests.Bootstrap;

public sealed class PipelineContributionStoreTests
{
    [Fact]
    public void Ignores_null_contribution()
    {
        ResetStore();

        PipelineContributionStore.Add((Action<IServiceCollection>)null!);

        var contributions = PipelineContributionStore.Drain();

        Assert.Empty(contributions);
    }

    [Fact]
    public void Stores_contribution()
    {
        ResetStore();
        Action<IServiceCollection> contribution = AddTestService;

        PipelineContributionStore.Add(contribution);

        var contributions = PipelineContributionStore.Drain();

        Assert.Single(contributions);
        var services = new ServiceCollection();

        contributions[0].Apply(services);

        AssertSingleRegistration<TestService>(services);
    }

    [Fact]
    public void Returns_all_contributions_in_insertion_order()
    {
        ResetStore();
        Action<IServiceCollection> first = AddFirstService;
        Action<IServiceCollection> second = AddSecondService;

        PipelineContributionStore.Add(first);
        PipelineContributionStore.Add(second);

        var contributions = PipelineContributionStore.Drain();

        Assert.Equal(2, contributions.Length);
        var services = new ServiceCollection();

        contributions[0].Apply(services);
        contributions[1].Apply(services);

        AssertSingleRegistration<FirstService>(services);
        AssertSingleRegistration<SecondService>(services);
    }

    [Fact]
    public void Returns_snapshot_of_current_contributions()
    {
        ResetStore();
        Action<IServiceCollection> first = AddFirstService;
        Action<IServiceCollection> second = AddSecondService;

        PipelineContributionStore.Add(first);
        var snapshot = PipelineContributionStore.Drain();

        PipelineContributionStore.Add(second);

        Assert.Single(snapshot);
        var services = new ServiceCollection();

        snapshot[0].Apply(services);

        AssertSingleRegistration<FirstService>(services);
    }

    [Fact]
    public void Does_not_clear_contributions_when_drained()
    {
        ResetStore();
        Action<IServiceCollection> contribution = AddTestService;

        PipelineContributionStore.Add(contribution);

        var firstDrain = PipelineContributionStore.Drain();
        var secondDrain = PipelineContributionStore.Drain();

        Assert.Single(firstDrain);
        Assert.Single(secondDrain);

        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();

        firstDrain[0].Apply(firstServices);
        secondDrain[0].Apply(secondServices);

        AssertSingleRegistration<TestService>(firstServices);
        AssertSingleRegistration<TestService>(secondServices);
    }

    private static void ResetStore()
        => PipelineContributionStore.ResetForTests();

    private static void AddTestService(IServiceCollection services)
        => services.AddSingleton<TestService>();

    private static void AddFirstService(IServiceCollection services)
        => services.AddSingleton<FirstService>();

    private static void AddSecondService(IServiceCollection services)
        => services.AddSingleton<SecondService>();

    private static void AssertSingleRegistration<TService>(IServiceCollection services)
    {
        var count = 0;

        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(TService))
                count++;
        }

        Assert.Equal(1, count);
    }

    private sealed class TestService;

    private sealed class FirstService;

    private sealed class SecondService;
}
