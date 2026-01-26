using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher;

/// <summary>
/// Default TinyDispatcher runtime dispatcher.
/// - Resolves handlers via registry (fast, no reflection on hot path)
/// - Creates TContext via IContextFactory<TContext> per dispatch call
/// - Optionally resolves generated pipeline per closed TCommand (cached, may be null)
/// </summary>
public sealed class Dispatcher<TContext> : IDispatcher<TContext>
{
    private readonly IServiceProvider _services;
    private readonly IDispatcherRegistry _registry;

    public Dispatcher(IServiceProvider services, IDispatcherRegistry registry)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public async Task DispatchAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        if (!_registry.CommandHandlers.TryGetValue(typeof(TCommand), out var handlerType))
        {
            throw new InvalidOperationException(
                $"No handler registered for command '{typeof(TCommand).FullName}'.");
        }

        var handlerObj = _services.GetRequiredService(handlerType);
        var handler = (ICommandHandler<TCommand, TContext>)handlerObj;

        var ctx = await _services.GetRequiredService<IContextFactory<TContext>>()
            .CreateAsync(ct)
            .ConfigureAwait(false);

        var pipeline = GetBestPipeline<TCommand>();
        if (pipeline is not null)
        {
            await pipeline.ExecuteAsync(command, ctx, handler, ct).ConfigureAwait(false);
            return;
        }

        await handler.HandleAsync(command, ctx, ct).ConfigureAwait(false);
    }

    public Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>
    {
        if (query is null) throw new ArgumentNullException(nameof(query));

        if (!_registry.QueryHandlers.TryGetValue(typeof(TQuery), out var handlerType))
        {
            throw new InvalidOperationException(
                $"No handler registered for query '{typeof(TQuery).FullName}'.");
        }

        var handler = (IQueryHandler<TQuery, TResult>)_services.GetRequiredService(handlerType);
        return handler.HandleAsync(query, ct);
    }

    private ICommandPipelineInvoker<TCommand, TContext>? GetBestPipeline<TCommand>()
        where TCommand : ICommand
    {
        // 1) per-command override
        var p1 = _services.GetService<ICommandPipeline<TCommand, TContext>>();
        if (p1 is not null) return p1;

        // 2) policy
        var p2 = _services.GetService<IPolicyCommandPipeline<TCommand, TContext>>();
        if (p2 is not null) return p2;

        // 3) global
        return _services.GetService<IGlobalCommandPipeline<TCommand, TContext>>();
    }
}
