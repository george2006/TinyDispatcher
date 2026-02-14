// File: tests/TinyDispatcher.UnitTests/TestDomain.cs
// CLEANED UP for single pipeline contract (ICommandPipeline<TCommand,TContext> only)
//
// Changes:
// - Removed unused usings
// - Removed PolicyPipeline / GlobalPipeline test doubles (they don't make sense with single pipeline contract)
// - Simplified CallTracker (only what runtime tests can assert now)
// - Kept SourceGen-related policy + middlewares (Global/Policy/PerCommand) for execution-order tests
// - Kept handlers + context factory
// - Kept CommandPipeline (used by runtime DI tests)

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher.UnitTests
{
    public sealed class CallTracker
    {
        public bool PipelineCalled { get; set; }
        public bool HandlerCalled { get; set; }
    }

    public sealed record TestCommand(string Value) : ICommand;
    public sealed record OtherCommand(string Value) : ICommand;
    public sealed record PolicyOnlyCommand(string Value) : ICommand;

    // -------------------------------------------------------------------------
    // Policy declarations (attributes read by SourceGen)
    // -------------------------------------------------------------------------

    [TinyPolicy]
    [UseMiddleware(typeof(PolicyLogMiddleware<,>))]
    [ForCommand(typeof(PolicyOnlyCommand))]
    internal sealed class PolicyOnlyPolicy { }

    [TinyPolicy]
    [UseMiddleware(typeof(PolicyLogMiddleware<,>))]
    [ForCommand(typeof(TestCommand))]
    internal sealed class CheckoutPolicy { }

    // -------------------------------------------------------------------------
    // Test context (we assert exact execution order in SourceGen tests)
    // -------------------------------------------------------------------------
    public sealed class TestContext
    {
        public List<string> Log { get; } = new();
        public CancellationToken SeenByFactory { get; internal set; }
        public CancellationToken SeenByHandler { get; internal set; }
    }

    // -------------------------------------------------------------------------
    // Context factory
    // -------------------------------------------------------------------------
    public sealed class TestContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken ct = default)
            => new(new TestContext { SeenByFactory = ct });
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------
    internal sealed class OtherCommandHandler : ICommandHandler<OtherCommand, TestContext>
    {
        public Task HandleAsync(OtherCommand command, TestContext ctx, CancellationToken ct = default)
        {
            ctx.Log.Add("handler:OtherCommand");
            return Task.CompletedTask;
        }
    }

    internal sealed class PolicyOnlyCommandHandler : ICommandHandler<PolicyOnlyCommand, TestContext>
    {
        public Task HandleAsync(PolicyOnlyCommand command, TestContext ctx, CancellationToken ct = default)
        {
            ctx.Log.Add("handler:PolicyOnlyCommand");
            return Task.CompletedTask;
        }
    }

    public sealed class TestHandler : ICommandHandler<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public TestHandler(CallTracker tracker) => _tracker = tracker;

        public Task HandleAsync(TestCommand command, TestContext ctx, CancellationToken ct = default)
        {
            _tracker.HandlerCalled = true;
            ctx.SeenByHandler = ct;
            ctx.Log.Add("handler:TestCommand");
            return Task.CompletedTask;
        }
    }

    // -------------------------------------------------------------------------
    // Runtime pipeline test double (used by non-sourcegen runtime tests)
    // -------------------------------------------------------------------------
    public sealed class CommandPipeline : ICommandPipeline<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public CommandPipeline(CallTracker tracker) => _tracker = tracker;

        public ValueTask ExecuteAsync(
            TestCommand command,
            TestContext ctx,
            ICommandHandler<TestCommand, TestContext> handler,
            CancellationToken ct = default)
        {
            _tracker.PipelineCalled = true;
            return new ValueTask(handler.HandleAsync(command, ctx, ct));
        }
    }

    // -------------------------------------------------------------------------
    // Open-generic middlewares (SourceGen expects open generic types)
    // -------------------------------------------------------------------------
    internal sealed class GlobalLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:global:before");
            await runtime.NextAsync(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:global:after");
        }
    }

    internal sealed class PolicyLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:policy:before");
            await runtime.NextAsync(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:policy:after");
        }
    }

    internal sealed class PerCommandLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async ValueTask InvokeAsync(
            TCommand command,
            TContext ctx,
            ICommandPipelineRuntime<TCommand, TContext> runtime,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:percmd:before");
            await runtime.NextAsync(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:percmd:after");
        }
    }
}
