using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyDispatcher.Context;

namespace TinyDispatcher.UnitTests
{
    public sealed class CallTracker
    {
        public bool CommandPipelineCalled { get; set; }
        public bool PolicyPipelineCalled { get; set; }
        public bool GlobalPipelineCalled { get; set; }
        public bool HandlerCalled { get; set; }
    }

    public sealed record TestCommand(string value) : ICommand;
    public sealed record OtherCommand(string Value) : ICommand;

    public sealed record PolicyOnlyCommand(string Value) : ICommand;

    [TinyPolicy]
    [UseMiddleware(typeof(PolicyLogMiddleware<,>))]
    [ForCommand(typeof(PolicyOnlyCommand))]
    internal sealed class PolicyOnlyPolicy { }

    // -----------------------------------------------------------------------------
    // Test context (we want to assert exact execution order)
    // -----------------------------------------------------------------------------
    public sealed class TestContext
    {
        public List<string> Log { get; } = new();
        public CancellationToken SeenByFactory { get; internal set; }
        public CancellationToken SeenByHandler { get; internal set; }
    }

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


    // -----------------------------------------------------------------------------
    // Open-generic middlewares (generator expects open generic types)
    // -----------------------------------------------------------------------------
    internal sealed class GlobalLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:global:before");
            await next(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:global:after");
        }
    }

    internal sealed class PerCommandLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:percmd:before");
            await next(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:percmd:after");
        }
    }
    public sealed class TestContextFactory : IContextFactory<TestContext>
    {
        public ValueTask<TestContext> CreateAsync(CancellationToken ct = default)
            => new(new TestContext());
    }

    public sealed class TestHandler : ICommandHandler<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public TestHandler(CallTracker tracker) => _tracker = tracker;

        public Task HandleAsync(TestCommand command, TestContext ctx, CancellationToken ct = default)
        {
            _tracker.HandlerCalled = true;
            ctx.Log.Add("handler:TestCommand");
            return Task.CompletedTask;
        }
    }

    public sealed class CommandPipeline : ICommandPipeline<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public CommandPipeline(CallTracker tracker) => _tracker = tracker;

        public Task ExecuteAsync(
            TestCommand command,
            TestContext ctx,
            ICommandHandler<TestCommand, TestContext> handler,
            CancellationToken ct = default)
        {
            _tracker.CommandPipelineCalled = true;
            return handler.HandleAsync(command, ctx, ct);
        }
    }

    public sealed class PolicyPipeline : IPolicyCommandPipeline<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public PolicyPipeline(CallTracker tracker) => _tracker = tracker;

        public Task ExecuteAsync(
            TestCommand command,
            TestContext ctx,
            ICommandHandler<TestCommand, TestContext> handler,
            CancellationToken ct = default)
        {
            _tracker.PolicyPipelineCalled = true;
            return handler.HandleAsync(command, ctx, ct);
        }
    }

    public sealed class GlobalPipeline : IGlobalCommandPipeline<TestCommand, TestContext>
    {
        private readonly CallTracker _tracker;

        public GlobalPipeline(CallTracker tracker) => _tracker = tracker;

        public Task ExecuteAsync(
            TestCommand command,
            TestContext ctx,
            ICommandHandler<TestCommand, TestContext> handler,
            CancellationToken ct = default)
        {
            _tracker.GlobalPipelineCalled = true;
            return handler.HandleAsync(command, ctx, ct);
        }
    }

    internal sealed class PolicyLogMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public async Task InvokeAsync(
            TCommand command,
            TContext ctx,
            CommandDelegate<TCommand, TContext> next,
            CancellationToken ct)
        {
            ((TestContext)(object)ctx).Log.Add("mw:policy:before");
            await next(command, ctx, ct);
            ((TestContext)(object)ctx).Log.Add("mw:policy:after");
        }
    }

    // -----------------------------------------------------------------------------
    // Policy declaration (attributes read by SourceGen)
    // -----------------------------------------------------------------------------
    [TinyPolicy]
    [UseMiddleware(typeof(PolicyLogMiddleware<,>))]
    [ForCommand(typeof(TestCommand))]
    internal sealed class CheckoutPolicy
    {
    }
}
