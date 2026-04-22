#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Bootstrap;
using Xunit;

namespace TinyDispatcher.UnitTests.Bootstrap;

[Collection("Pipeline contribution store")]
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

        DispatcherPipelineBootstrap.AddContribution((Action<IServiceCollection>)null!);

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
    public void Applies_registered_object_contribution()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(new TestContribution());

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

    [Fact]
    public void Stores_contributed_command_handler_metadata_snapshot()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(new TestContribution());

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        var snapshot = GetCommandHandlersSnapshot(services);

        Assert.Single(snapshot);
        Assert.Equal("global::MyApp.CreateOrder", snapshot[0].CommandTypeFqn);
        Assert.Equal("global::MyApp.CreateOrderHandler", snapshot[0].HandlerTypeFqn);
        Assert.Equal("global::MyApp.AppContext", snapshot[0].ContextTypeFqn);
    }

    [Fact]
    public void Stores_empty_command_handler_snapshot_for_delegate_contribution()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(AddTestService);

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        var snapshot = GetCommandHandlersSnapshot(services);

        Assert.Empty(snapshot);
    }

    [Fact]
    public void Deduplicates_identical_contributed_command_handler_metadata()
    {
        ResetStore();
        var descriptor = CreateDescriptor(
            commandTypeFqn: "global::MyApp.CreateOrder",
            handlerTypeFqn: "global::MyApp.CreateOrderHandler",
            contextTypeFqn: "global::MyApp.AppContext");

        DispatcherPipelineBootstrap.AddContribution(new TestContribution(descriptor));
        DispatcherPipelineBootstrap.AddContribution(new TestContribution(descriptor));

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        var snapshot = GetCommandHandlersSnapshot(services);

        Assert.Single(snapshot);
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

    private static IReadOnlyList<CommandHandlerDescriptor> GetCommandHandlersSnapshot(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(IReadOnlyList<CommandHandlerDescriptor>))
                continue;

            return Assert.IsAssignableFrom<IReadOnlyList<CommandHandlerDescriptor>>(descriptor.ImplementationInstance);
        }

        throw new InvalidOperationException("Expected contributed command handler snapshot registration.");
    }

    private static void ResetStore()
        => PipelineContributionStore.ResetForTests();

    private static CommandHandlerDescriptor CreateDescriptor(
        string commandTypeFqn,
        string handlerTypeFqn,
        string contextTypeFqn)
        => new(
            CommandTypeFqn: commandTypeFqn,
            HandlerTypeFqn: handlerTypeFqn,
            ContextTypeFqn: contextTypeFqn);

    private sealed class TestService;
    private sealed class TestContribution : IDispatcherAssemblyContribution
    {
        public TestContribution()
            : this(CreateDescriptor(
                commandTypeFqn: "global::MyApp.CreateOrder",
                handlerTypeFqn: "global::MyApp.CreateOrderHandler",
                contextTypeFqn: "global::MyApp.AppContext"))
        {
        }

        public TestContribution(params CommandHandlerDescriptor[] commandHandlers)
        {
            CommandHandlers = commandHandlers;
        }

        public IReadOnlyList<CommandHandlerDescriptor> CommandHandlers { get; }

        public void Apply(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
        }
    }
}
