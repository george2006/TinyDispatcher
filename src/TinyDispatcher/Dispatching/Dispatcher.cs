using System;
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
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task DispatchAsync<TCommand>(TCommand command, CancellationToken ct = default)
        where TCommand : ICommand
    {
        if (command is null) throw new ArgumentNullException(nameof(command));

        using var activity = DispatcherTelemetry.StartCommand<TCommand>();

        try
        {
            var handler = _services.GetRequiredService<ICommandHandler<TCommand, TContext>>();
            DispatcherTelemetry.SetHandler(activity, handler.GetType());

            var ctx = await _contextFactory.CreateAsync(ct).ConfigureAwait(false);
            var pipeline = _services.GetService<ICommandPipeline<TCommand, TContext>>();

            if (pipeline is null)
            {
                await handler.HandleAsync(command, ctx, ct).ConfigureAwait(false);
            }
            else
            {
                await pipeline.ExecuteAsync(command, ctx, handler, ct).ConfigureAwait(false);
            }

            DispatcherTelemetry.CompleteSuccessfully(activity);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DispatcherTelemetry.CompleteAsCanceled(activity);
            throw;
        }
        catch (Exception exception)
        {
            DispatcherTelemetry.CompleteWithFailure(activity, exception);
            throw;
        }
    }

    public async Task<TResult> DispatchAsync<TQuery, TResult>(TQuery query, CancellationToken ct = default)
        where TQuery : IQuery<TResult>
    {
        if (query is null) throw new ArgumentNullException(nameof(query));

        using var activity = DispatcherTelemetry.StartQuery<TQuery>();

        try
        {
            var handler = _services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
            DispatcherTelemetry.SetHandler(activity, handler.GetType());

            var result = await handler.HandleAsync(query, ct).ConfigureAwait(false);

            DispatcherTelemetry.CompleteSuccessfully(activity);

            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            DispatcherTelemetry.CompleteAsCanceled(activity);
            throw;
        }
        catch (Exception exception)
        {
            DispatcherTelemetry.CompleteWithFailure(activity, exception);
            throw;
        }
    }
}
