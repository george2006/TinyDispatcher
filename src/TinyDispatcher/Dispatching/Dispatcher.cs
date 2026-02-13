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
    private readonly IContextFactory<TContext> _contextFactory;

    public Dispatcher(IServiceProvider services, IContextFactory<TContext> contextFactory)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(_contextFactory));
    }

    public async Task DispatchAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        var handler = _services.GetRequiredService<ICommandHandler<TCommand, TContext>>();

        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for command '{typeof(TCommand).FullName}'.");
        }

        var ctx = await _contextFactory.CreateAsync(ct).ConfigureAwait(false);
            
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

        var handler = _services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
        if (handler == null)
        {
            throw new InvalidOperationException(
                $"No handler registered for query '{typeof(TQuery).FullName}'.");
        }
        return handler.HandleAsync(query, ct);
    }

    private ICommandPipelineInvoker<TCommand, TContext>? GetBestPipeline<TCommand>()
    where TCommand : ICommand
    {
        ICommandPipelineInvoker<TCommand, TContext>? p =
            _services.GetService<ICommandPipeline<TCommand, TContext>>();

        if (p is null)
            p = _services.GetService<IPolicyCommandPipeline<TCommand, TContext>>();

        if (p is null)
            p = _services.GetService<IGlobalCommandPipeline<TCommand, TContext>>();

        return p;
    }
}
