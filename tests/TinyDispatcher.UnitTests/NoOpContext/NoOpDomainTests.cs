using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.UnitTests.NoOp;

// -------------------------------------------------------------------------
// Commands
// -------------------------------------------------------------------------

public sealed record NoOpTestCommand(string Value) : ICommand;
public sealed record NoOpOtherCommand(string Value) : ICommand;

// -------------------------------------------------------------------------
// Call tracker (since NoOpContext has no state)
// -------------------------------------------------------------------------

public sealed class NoOpCallTracker
{
    public bool GlobalMiddlewareCalled { get; set; }
    public bool PerCommandMiddlewareCalled { get; set; }
    public bool HandlerCalled { get; set; }
}

// -------------------------------------------------------------------------
// Handlers
// -------------------------------------------------------------------------

internal sealed class NoOpOtherHandler
    : ICommandHandler<NoOpOtherCommand, NoOpContext>
{
    public Task HandleAsync(
        NoOpOtherCommand command,
        NoOpContext ctx,
        CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class NoOpTestHandler
    : ICommandHandler<NoOpTestCommand, NoOpContext>
{
    private readonly NoOpCallTracker _tracker;

    public NoOpTestHandler(NoOpCallTracker tracker)
        => _tracker = tracker;

    public Task HandleAsync(
        NoOpTestCommand command,
        NoOpContext ctx,
        CancellationToken ct = default)
    {
        _tracker.HandlerCalled = true;
        return Task.CompletedTask;
    }
}

// -------------------------------------------------------------------------
// Open generic middlewares
// -------------------------------------------------------------------------

internal sealed class NoOpGlobalMiddleware<TCommand, TContext>
    : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    private readonly NoOpCallTracker _tracker;

    public NoOpGlobalMiddleware(NoOpCallTracker tracker)
        => _tracker = tracker;

    public async ValueTask InvokeAsync(
        TCommand command,
        TContext ctx,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        _tracker.GlobalMiddlewareCalled = true;
        await runtime.NextAsync(command, ctx, ct);
    }
}

internal sealed class NoOpPerCommandMiddleware<TCommand, TContext>
    : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    private readonly NoOpCallTracker _tracker;

    public NoOpPerCommandMiddleware(NoOpCallTracker tracker)
        => _tracker = tracker;

    public async ValueTask InvokeAsync(
        TCommand command,
        TContext ctx,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        _tracker.PerCommandMiddlewareCalled = true;
        await runtime.NextAsync(command, ctx, ct);
    }
}