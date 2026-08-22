using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using Xunit;

namespace TinyDispatcher.UnitTests;

public sealed class MiddlewareDeclarationOrderTests
{
    [Fact]
    public async Task Global_middlewares_run_in_declaration_order()
    {
        var services = new ServiceCollection();
        var context = new DeclarationOrderContext();

        services.AddSingleton<IContextFactory<DeclarationOrderContext>>(
            new DeclarationOrderContextFactory(context));
        TinyDispatcher.Generated.ThisAssemblyContribution.AddServices(services);
        services.AddScoped<IDispatcher<DeclarationOrderContext>>(provider =>
            new Dispatcher<DeclarationOrderContext>(
                provider,
                provider.GetRequiredService<IContextFactory<DeclarationOrderContext>>()));

        await using var provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher<DeclarationOrderContext>>();

        await dispatcher.DispatchAsync(new DeclarationOrderCommand());

        Assert.Equal(
            new[]
            {
                "zulu:before",
                "alpha:before",
                "handler",
                "alpha:after",
                "zulu:after",
            },
            context.Events);
    }
}

internal static class MiddlewareDeclarationOrderHostGate
{
    public static void Configure(IServiceCollection services)
    {
        services.UseTinyDispatcher<DeclarationOrderContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(ZuluMiddleware<,>));
            tiny.UseGlobalMiddleware(typeof(AlphaMiddleware<,>));
        });
    }
}

internal sealed record DeclarationOrderCommand : ICommand;

internal sealed class DeclarationOrderContext
{
    public List<string> Events { get; } = new();
}

internal sealed class DeclarationOrderContextFactory : IContextFactory<DeclarationOrderContext>
{
    private readonly DeclarationOrderContext _context;

    public DeclarationOrderContextFactory(DeclarationOrderContext context)
    {
        _context = context;
    }

    public ValueTask<DeclarationOrderContext> CreateAsync(CancellationToken ct = default)
    {
        return ValueTask.FromResult(_context);
    }
}

internal sealed class DeclarationOrderCommandHandler :
    ICommandHandler<DeclarationOrderCommand, DeclarationOrderContext>
{
    public Task HandleAsync(
        DeclarationOrderCommand command,
        DeclarationOrderContext context,
        CancellationToken ct = default)
    {
        context.Events.Add("handler");
        return Task.CompletedTask;
    }
}

internal sealed class ZuluMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        var orderContext = (DeclarationOrderContext)(object)context!;

        orderContext.Events.Add("zulu:before");
        await runtime.NextAsync(command, context, ct);
        orderContext.Events.Add("zulu:after");
    }
}

internal sealed class AlphaMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        var orderContext = (DeclarationOrderContext)(object)context!;

        orderContext.Events.Add("alpha:before");
        await runtime.NextAsync(command, context, ct);
        orderContext.Events.Add("alpha:after");
    }
}
