using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using Xunit;

namespace TinyDispatcher.UnitTests.DependencyInjection;

public sealed class ServiceCollectionExtensionsTests
{
    private sealed class DummyContext
    {
        public string Value { get; }

        public DummyContext(string value)
        {
            Value = value;
        }
    }

    private sealed class DummyDependency
    {
    }

    private sealed class SelectedFactoryContext
    {
        public string Value { get; }

        public SelectedFactoryContext(string value)
        {
            Value = value;
        }
    }

    private sealed class DelegateFactoryContext
    {
        public string Value { get; }

        public DelegateFactoryContext(string value)
        {
            Value = value;
        }
    }

    private sealed class InvalidDefaultFactoryContext
    {
    }

    private sealed class WrongFactoryContext
    {
    }

    private sealed class FakeContextFactory : IContextFactory<DummyContext>
    {
        private readonly DummyContext _context;

        public FakeContextFactory(DummyContext context)
        {
            _context = context;
        }

        public ValueTask<DummyContext> CreateAsync(CancellationToken ct = default)
            => new(_context);
    }

    private sealed class SelectedContextFactory : IContextFactory<SelectedFactoryContext>
    {
        public ValueTask<SelectedFactoryContext> CreateAsync(CancellationToken ct = default)
            => new(new SelectedFactoryContext("selected_factory"));
    }

    private sealed class ReplacedContextFactory : IContextFactory<DelegateFactoryContext>
    {
        public ValueTask<DelegateFactoryContext> CreateAsync(CancellationToken ct = default)
            => new(new DelegateFactoryContext("selected_factory"));
    }

    private sealed class WrongContextFactory
    {
    }

    [Fact]
    public void Add_dispatcher_when_services_is_null_throws_argument_null_exception()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            ServiceCollectionExtensions.AddDispatcher<DummyContext>(null!));

        Assert.Equal("services", exception.ParamName);
    }

    [Fact]
    public void Add_dispatcher_when_no_context_factory_registered_throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddDispatcher<DummyContext>());

        Assert.Equal(
            "TinyDispatcher: no context factory is registered for the requested context type. " +
            "Either register IContextFactory<TContext> before calling AddDispatcher, " +
            "or pass a contextFactory callback to AddDispatcher<TContext>(..., contextFactory: ...).",
            exception.Message);
    }

    [Fact]
    public void Add_dispatcher_when_context_factory_is_registered_registers_dispatcher()
    {
        var services = new ServiceCollection();

        services.AddScoped<IContextFactory<DummyContext>>(_ =>
            new FakeContextFactory(new DummyContext("registered")));

        var result = services.AddDispatcher<DummyContext>();

        Assert.Same(services, result);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetService<IDispatcher<DummyContext>>();
        var factory = scope.ServiceProvider.GetService<IContextFactory<DummyContext>>();

        Assert.NotNull(dispatcher);
        Assert.NotNull(factory);
    }

    [Fact]
    public async Task Add_dispatcher_when_delegate_context_factory_is_provided_registers_factory()
    {
        var services = new ServiceCollection();
        services.AddScoped<DummyDependency>();

        CancellationToken capturedCancellationToken = default;
        DummyDependency? capturedDependency = null;

        services.AddDispatcher<DummyContext>((sp, ct) =>
        {
            capturedCancellationToken = ct;
            capturedDependency = sp.GetRequiredService<DummyDependency>();
            return new ValueTask<DummyContext>(new DummyContext("from_delegate"));
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IContextFactory<DummyContext>>();
        var token = new CancellationTokenSource().Token;

        var context = await factory.CreateAsync(token);

        Assert.Equal("from_delegate", context.Value);
        Assert.Equal(token, capturedCancellationToken);
        Assert.NotNull(capturedDependency);
    }

    [Fact]
    public async Task Add_dispatcher_when_delegate_context_factory_is_provided_replaces_existing_factory()
    {
        var services = new ServiceCollection();

        services.AddScoped<IContextFactory<DummyContext>>(_ =>
            new FakeContextFactory(new DummyContext("old_factory")));

        services.AddDispatcher<DummyContext>((_, _) =>
            new ValueTask<DummyContext>(new DummyContext("new_factory")));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IContextFactory<DummyContext>>();
        var context = await factory.CreateAsync();

        Assert.Equal("new_factory", context.Value);
    }

    [Fact]
    public async Task Use_tiny_dispatcher_when_factory_type_is_selected_registers_factory()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<SelectedFactoryContext>(tiny =>
        {
            tiny.UseContextFactory<SelectedContextFactory>();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IContextFactory<SelectedFactoryContext>>();
        var context = await factory.CreateAsync();

        Assert.Equal("selected_factory", context.Value);
    }

    [Fact]
    public async Task Use_tiny_dispatcher_when_delegate_factory_is_provided_replaces_selected_factory()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<DelegateFactoryContext>(
            tiny =>
            {
                tiny.UseContextFactory<ReplacedContextFactory>();
            },
            (_, _) => new ValueTask<DelegateFactoryContext>(new DelegateFactoryContext("delegate_factory")));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IContextFactory<DelegateFactoryContext>>();
        var context = await factory.CreateAsync();

        Assert.Equal("delegate_factory", context.Value);
    }

    [Fact]
    public async Task Use_tiny_dispatcher_when_default_factory_is_selected_for_app_context_registers_default_factory()
    {
        var services = new ServiceCollection();

        services.UseTinyDispatcher<AppContext>(tiny =>
        {
            tiny.UseDefaultFactory();
        });

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IContextFactory<AppContext>>();
        var context = await factory.CreateAsync();

        Assert.NotNull(context);
    }

    [Fact]
    public void Use_tiny_dispatcher_when_default_factory_is_selected_for_custom_context_throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.UseTinyDispatcher<InvalidDefaultFactoryContext>(tiny =>
            {
                tiny.UseDefaultFactory();
            }));

        Assert.Equal(
            "TinyDispatcher: UseDefaultFactory() is only available for TinyDispatcher.AppContext.",
            exception.Message);
    }

    [Fact]
    public void Use_tiny_dispatcher_when_selected_factory_does_not_match_context_throws()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.UseTinyDispatcher<WrongFactoryContext>(tiny =>
            {
                tiny.UseContextFactory<WrongContextFactory>();
            }));

        Assert.Contains("must implement", exception.Message);
        Assert.Contains(nameof(WrongContextFactory), exception.Message);
    }
}
