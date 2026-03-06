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
        Assert.Same(contribution, contributions[0]);
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
        Assert.Same(first, contributions[0]);
        Assert.Same(second, contributions[1]);
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
        Assert.Same(first, snapshot[0]);
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
        Assert.Same(contribution, firstDrain[0]);
        Assert.Same(contribution, secondDrain[0]);
    }

    private static void ResetStore()
        => PipelineContributionStore.ResetForTests();

    private static void AddTestService(IServiceCollection services)
        => services.AddSingleton<TestService>();

    private static void AddFirstService(IServiceCollection services)
        => services.AddSingleton<FirstService>();

    private static void AddSecondService(IServiceCollection services)
        => services.AddSingleton<SecondService>();

    private sealed class TestService;

    private sealed class FirstService;

    private sealed class SecondService;
}