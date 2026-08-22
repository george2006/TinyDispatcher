#nullable enable

using System;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Bootstrap;
using Xunit;
using GeneratedTestCommand = TinyDispatcher.UnitTests.TestCommand;
using GeneratedTestContext = TinyDispatcher.UnitTests.TestContext;
using GeneratedTestHandler = TinyDispatcher.UnitTests.TestHandler;

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

        DispatcherPipelineBootstrap.AddContribution(null!);

        var exception = Record.Exception(() => DispatcherPipelineBootstrap.Apply(services));

        Assert.Null(exception);
    }

    [Fact]
    public void Applies_registered_contribution()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(registerServices: AddTestService));

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
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(registerServices: AddTestService));

        var services = CreateServices();

        DispatcherPipelineBootstrap.Apply(services);
        DispatcherPipelineBootstrap.Apply(services);

        AssertSingleRegistration<TestService>(services);
    }

    [Fact]
    public void Applies_stored_contributions_to_each_service_collection()
    {
        ResetStore();
        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(registerServices: AddTestService));

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
    public void Returns_operations_from_all_registered_contributions_without_applying_them()
    {
        var command = new DispatcherOperationStructure(
            typeof(TestCommand),
            typeof(TestCommandHandler),
            DispatcherOperationKind.Command,
            typeof(TestContext));
        var query = new DispatcherOperationStructure(
            typeof(TestQuery),
            typeof(TestQueryHandler),
            DispatcherOperationKind.Query);

        DispatcherPipelineBootstrap.AddContribution(CreateContribution(command));
        DispatcherPipelineBootstrap.AddContribution(CreateContribution(query));

        var operations = DispatcherPipelineBootstrap.GetOperations();

        Assert.Equal(new[] { command, query }, operations);
    }

    [Fact]
    public void Returns_a_new_operation_snapshot_for_each_read()
    {
        var operation = new DispatcherOperationStructure(
            typeof(TestCommand),
            typeof(TestCommandHandler),
            DispatcherOperationKind.Command,
            typeof(TestContext));

        DispatcherPipelineBootstrap.AddContribution(CreateContribution(operation));

        var first = DispatcherPipelineBootstrap.GetOperations();
        var second = DispatcherPipelineBootstrap.GetOperations();

        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Returns_operations_in_deterministic_identity_order()
    {
        var query = new DispatcherOperationStructure(
            typeof(TestQuery),
            typeof(TestQueryHandler),
            DispatcherOperationKind.Query);
        var command = new DispatcherOperationStructure(
            typeof(TestCommand),
            typeof(TestCommandHandler),
            DispatcherOperationKind.Command,
            typeof(TestContext));

        DispatcherPipelineBootstrap.AddContribution(CreateContribution(query, command));

        var operations = DispatcherPipelineBootstrap.GetOperations();

        Assert.Equal(new[] { command, query }, operations);
    }

    [Fact]
    public void Retains_the_single_delegate_constructor_for_compiled_contributions()
    {
        var constructor = typeof(AssemblyContribution).GetConstructor(
            new[] { typeof(Action<IServiceCollection>) });

        Assert.NotNull(constructor);
    }

    [Fact]
    public void Rejects_an_unknown_operation_kind()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DispatcherOperationStructure(
                typeof(TestCommand),
                typeof(TestCommandHandler),
                (DispatcherOperationKind)42));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    public void Creates_operation_structure_only_when_first_requested()
    {
        var factoryCalls = 0;

        DispatcherPipelineBootstrap.AddContribution(new AssemblyContribution(
            registerServices: null,
            getOperations: () =>
            {
                factoryCalls++;
                return new[]
                {
                    new DispatcherOperationStructure(
                        typeof(TestCommand),
                        typeof(TestCommandHandler),
                        DispatcherOperationKind.Command,
                        typeof(TestContext))
                };
            }));

        var callsBeforeRead = factoryCalls;

        var first = DispatcherPipelineBootstrap.GetOperations();
        var second = DispatcherPipelineBootstrap.GetOperations();

        Assert.Equal(0, callsBeforeRead);
        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void Generated_contribution_exposes_real_operation_structure_without_dispatching()
    {
        DispatcherPipelineBootstrap.AddContribution(
            global::TinyDispatcher.Generated.ThisAssemblyContribution.Create());

        var operations = DispatcherPipelineBootstrap.GetOperations();

        var operation = Assert.Single(
            operations,
            candidate =>
                candidate.OperationType == typeof(GeneratedTestCommand) &&
                candidate.ContextType == typeof(GeneratedTestContext));

        Assert.Equal(typeof(GeneratedTestHandler), operation.HandlerType);
        Assert.Equal(DispatcherOperationKind.Command, operation.Kind);
    }

    private static AssemblyContribution CreateContribution(
        params DispatcherOperationStructure[] operations)
    {
        return new AssemblyContribution(
            registerServices: null,
            getOperations: () => operations);
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

    private sealed class TestCommand;

    private sealed class TestCommandHandler;

    private sealed class TestQuery;

    private sealed class TestQueryHandler;

    private sealed class TestContext;
}
