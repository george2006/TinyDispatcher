#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher;
using Xunit;

namespace TinyDispatcher.UnitTets;

public sealed class DispatcherPipelineBootstrapTests
{
    [Fact]
    public void Apply_is_idempotent_per_IServiceCollection()
    {
        var calls = 0;

        DispatcherPipelineBootstrap.AddContribution(_ => calls++);
        DispatcherPipelineBootstrap.AddContribution(_ => calls++);

        var services = new ServiceCollection();

        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);

        Assert.Equal(2, calls);
    }

    [Fact]
    public void Apply_throws_if_services_null()
    {
        Assert.Throws<ArgumentNullException>(() => DispatcherPipelineBootstrap.Apply(null!));
    }
}
