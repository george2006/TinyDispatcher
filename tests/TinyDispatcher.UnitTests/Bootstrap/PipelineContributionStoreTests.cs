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

        PipelineContributionStore.Add(null!);

        var contributions = PipelineContributionStore.GetSnapshot();

        Assert.Empty(contributions);
    }

    [Fact]
    public void Stores_contribution()
    {
        ResetStore();
        var contribution = new AssemblyContribution(registerServices: AddTestService);

        PipelineContributionStore.Add(contribution);

        var contributions = PipelineContributionStore.GetSnapshot();

        Assert.Single(contributions);
        var services = new ServiceCollection();

        contributions[0].Apply(services);

        AssertSingleRegistration<TestService>(services);
    }

    [Fact]
    public void Returns_all_contributions_in_insertion_order()
    {
        ResetStore();
        var first = new AssemblyContribution(registerServices: AddFirstService);
        var second = new AssemblyContribution(registerServices: AddSecondService);

        PipelineContributionStore.Add(first);
        PipelineContributionStore.Add(second);

        var contributions = PipelineContributionStore.GetSnapshot();

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
        var first = new AssemblyContribution(registerServices: AddFirstService);
        var second = new AssemblyContribution(registerServices: AddSecondService);

        PipelineContributionStore.Add(first);
        var snapshot = PipelineContributionStore.GetSnapshot();

        PipelineContributionStore.Add(second);

        Assert.Single(snapshot);
        var services = new ServiceCollection();

        snapshot[0].Apply(services);

        AssertSingleRegistration<FirstService>(services);
    }

    [Fact]
    public void Does_not_clear_contributions_when_snapshotted()
    {
        ResetStore();
        var contribution = new AssemblyContribution(registerServices: AddTestService);

        PipelineContributionStore.Add(contribution);

        var firstSnapshot = PipelineContributionStore.GetSnapshot();
        var secondSnapshot = PipelineContributionStore.GetSnapshot();

        Assert.Single(firstSnapshot);
        Assert.Single(secondSnapshot);

        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();

        firstSnapshot[0].Apply(firstServices);
        secondSnapshot[0].Apply(secondServices);

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
