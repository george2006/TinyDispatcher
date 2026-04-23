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
    public DispatcherPipelineBootstrapTests()
    {
        ResetStore();
    }

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
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(registerServices: AddTestService));

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
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(
            contextType: typeof(AppContext),
            handlers: new[]
            {
                new HandlerBinding(typeof(CreateOrder), typeof(CreateOrderHandler), typeof(AppContext)),
            }));

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        var typedSnapshot = GetHandlerBindingsSnapshot(services);
        var snapshot = GetCommandHandlersSnapshot(services);

        var binding = Assert.Single(typedSnapshot);
        Assert.Equal(typeof(CreateOrder), binding.CommandType);
        Assert.Equal(typeof(CreateOrderHandler), binding.HandlerType);
        Assert.Equal(typeof(AppContext), binding.ContextType);

        Assert.Single(snapshot);
        Assert.Equal("global::TinyDispatcher.UnitTests.Bootstrap.DispatcherPipelineBootstrapTests.CreateOrder", snapshot[0].CommandTypeFqn);
        Assert.Equal("global::TinyDispatcher.UnitTests.Bootstrap.DispatcherPipelineBootstrapTests.CreateOrderHandler", snapshot[0].HandlerTypeFqn);
        Assert.Equal("global::TinyDispatcher.UnitTests.Bootstrap.DispatcherPipelineBootstrapTests.AppContext", snapshot[0].ContextTypeFqn);
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
        var handler = new HandlerBinding(typeof(CreateOrder), typeof(CreateOrderHandler), typeof(AppContext));

        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(handlers: new[] { handler }));
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(handlers: new[] { handler }));

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);

        var typedSnapshot = GetHandlerBindingsSnapshot(services);
        var snapshot = GetCommandHandlersSnapshot(services);

        Assert.Single(typedSnapshot);
        Assert.Single(snapshot);
    }

    [Fact]
    public void Stores_all_structured_contributions_for_future_composition()
    {
        ResetStore();

        var first = new AssemblyContribution(
            contextType: typeof(AppContext),
            registerServices: AddTestService,
            handlers: new[]
            {
                new HandlerBinding(typeof(CreateOrder), typeof(CreateOrderHandler), typeof(AppContext)),
            },
            pipelines: new[]
            {
                new PipelineBinding(commandType: null, middlewares: new[] { new MiddlewareBinding(typeof(GlobalMiddleware<,>)) }),
                new PipelineBinding(typeof(CreateOrder), new[] { new MiddlewareBinding(typeof(CommandMiddleware<,>)) }),
            },
            policies: new[]
            {
                new PolicyBinding(
                    typeof(CreateOrderPolicy),
                    new[] { new MiddlewareBinding(typeof(PolicyMiddleware<,>)) },
                    new[] { new PolicyCommandBinding(typeof(CreateOrder)) }),
            });

        var second = new AssemblyContribution(
            contextType: typeof(AppContext),
            handlers: new[]
            {
                new HandlerBinding(typeof(CancelOrder), typeof(CancelOrderHandler), typeof(AppContext)),
            });

        DispatcherPipelineBootstrap.AddContribution(first);
        DispatcherPipelineBootstrap.AddContribution(second);

        var services = CreateServices();
        DispatcherPipelineBootstrap.Apply(services);

        var contributions = GetAssemblyContributionSnapshot(services);

        Assert.Equal(2, contributions.Count);
        Assert.Equal(typeof(AppContext), contributions[0].ContextType);
        Assert.Equal(typeof(CreateOrder), Assert.Single(contributions[0].Handlers).CommandType);
        Assert.Equal(typeof(CancelOrder), Assert.Single(contributions[1].Handlers).CommandType);
        Assert.Equal(typeof(GlobalMiddleware<,>), Assert.Single(contributions[0].Pipelines[0].Middlewares).MiddlewareType);
        Assert.Equal(typeof(CreateOrderPolicy), Assert.Single(contributions[0].Policies).PolicyType);
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

    private static IReadOnlyList<AssemblyContribution> GetAssemblyContributionSnapshot(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(IReadOnlyList<AssemblyContribution>))
                continue;

            return Assert.IsAssignableFrom<IReadOnlyList<AssemblyContribution>>(descriptor.ImplementationInstance);
        }

        throw new InvalidOperationException("Expected structured contribution snapshot registration.");
    }

    private static IReadOnlyList<HandlerBinding> GetHandlerBindingsSnapshot(IServiceCollection services)
    {
        for (int i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (descriptor.ServiceType != typeof(IReadOnlyList<HandlerBinding>))
                continue;

            return Assert.IsAssignableFrom<IReadOnlyList<HandlerBinding>>(descriptor.ImplementationInstance);
        }

        throw new InvalidOperationException("Expected structured handler binding snapshot registration.");
    }

    private static void ResetStore()
        => PipelineContributionStore.ResetForTests();

    private sealed class TestService;
    private sealed class AppContext;
    private sealed class CreateOrder : ICommand;
    private sealed class CancelOrder : ICommand;
    private sealed class CreateOrderPolicy;
    private sealed class GlobalMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    private sealed class CommandMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    private sealed class PolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    private sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
    {
        public Task HandleAsync(CreateOrder command, AppContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CancelOrderHandler : ICommandHandler<CancelOrder, AppContext>
    {
        public Task HandleAsync(CancelOrder command, AppContext context, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
