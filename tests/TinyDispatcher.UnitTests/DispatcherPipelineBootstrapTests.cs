#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using TinyDispatcher.Bootstrap;
using Xunit;

namespace TinyDispatcher.UnitTests;

[Collection("Pipeline contribution store")]
public sealed class DispatcherPipelineBootstrapTests
{
    [Fact]
    public void Apply_is_idempotent_per_IServiceCollection()
    {
        PipelineContributionStore.ResetForTests();

        var calls = 0;

        DispatcherPipelineBootstrap.AddContribution(ContributionThatIncrementsCallCount());
        DispatcherPipelineBootstrap.AddContribution(ContributionThatIncrementsCallCount());

        var services = new ServiceCollection();

        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);

        Assert.Equal(2, calls);

        AssemblyContribution ContributionThatIncrementsCallCount()
            => new(registerServices: _ => calls++);
    }

    [Fact]
    public void Apply_throws_if_services_null()
    {
        Assert.Throws<ArgumentNullException>(() => DispatcherPipelineBootstrap.Apply(null!));
    }
}
