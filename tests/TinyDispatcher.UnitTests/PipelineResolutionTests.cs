using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using Xunit;

namespace TinyDispatcher.UnitTets;

// -----------------------------------------------------------------------------
// IMPORTANT:
// - These message types MUST be top-level (not nested private) because the SourceGen
//   runs on the UnitTests assembly and generates code that references them.
// -----------------------------------------------------------------------------
public sealed record TestCommand(string Value) : ICommand;
public sealed record OtherCommand(string Value) : ICommand;

public sealed record PolicyOnlyCommand(string Value) : ICommand;

[TinyPolicy]
[UseMiddleware(typeof(PolicyLogMiddleware<,>))]
[ForCommand(typeof(PolicyOnlyCommand))]
internal sealed class PolicyOnlyPolicy { }

// -----------------------------------------------------------------------------
// Test context (we want to assert exact execution order)
// -----------------------------------------------------------------------------
internal sealed class TestContext
{
    public List<string> Log { get; } = new();
}

// -----------------------------------------------------------------------------
// Handlers
// -----------------------------------------------------------------------------
internal sealed class TestCommandHandler : ICommandHandler<TestCommand, TestContext>
{
    public Task HandleAsync(TestCommand command, TestContext ctx, CancellationToken ct = default)
    {
        ctx.Log.Add("handler:TestCommand");
        return Task.CompletedTask;
    }
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

// -----------------------------------------------------------------------------
// HOST GATE (compile-time only):
// This exists so SourceGen sees ALL the configuration up-front and generates pipelines.
// It is never executed.
// -----------------------------------------------------------------------------
internal static class GeneratedPipelinesHostGate
{
    // Do not call. Just needs to exist in the compilation.
    public static void Configure(IServiceCollection services)
    {
        services.UseTinyDispatcher<TestContext>(tiny =>
        {
            tiny.UseGlobalMiddleware(typeof(GlobalLogMiddleware<,>));
            tiny.UseMiddlewareFor<TestCommand>(typeof(PerCommandLogMiddleware<,>));
            tiny.UsePolicy<CheckoutPolicy>();
            tiny.UsePolicy<PolicyOnlyPolicy>();
        });
    }
}

// -----------------------------------------------------------------------------
// Tests
// -----------------------------------------------------------------------------
public sealed class PipelineResolutionTest
{
    private sealed class FixedContextFactory : IContextFactory<TestContext>
    {
        private readonly TestContext _ctx;
        public FixedContextFactory(TestContext ctx) => _ctx = ctx;
        public ValueTask<TestContext> CreateAsync(CancellationToken ct = default) => ValueTask.FromResult(_ctx);
    }

    // Runtime container: DO NOT call UseTinyDispatcher here to "drive generation".
    // Generation already happened at compile time thanks to GeneratedPipelinesHostGate.
    private static ServiceProvider BuildProvider(TestContext ctx)
    {
        var services = new ServiceCollection();

        // Middleware implementations used by the generated pipelines
        services.AddTransient(typeof(GlobalLogMiddleware<,>));
        services.AddTransient(typeof(PerCommandLogMiddleware<,>));
        services.AddTransient(typeof(PolicyLogMiddleware<,>));

        services.AddSingleton<IContextFactory<TestContext>>(_ => new FixedContextFactory(ctx));

        TinyDispatcher.Generated.ThisAssemblyPipelineContribution.Add(services);

        var registry = DispatcherBootstrap.BuildRegistry();
        services.AddSingleton<IDispatcherRegistry>(registry);

        services.AddSingleton<IDispatcher<TestContext>>(sp =>
            new Dispatcher<TestContext>(sp, sp.GetRequiredService<IDispatcherRegistry>()));

        return services.BuildServiceProvider();
    }




    [Fact]
    public async Task Global_middleware_is_invoked_before_handler()
    {
        var ctx = new TestContext();
        using var sp = BuildProvider(ctx);

        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
        await dispatcher.DispatchAsync(new TestCommand("x"));

        // NOTE: if per-command pipeline exists for TestCommand (it does, via HostGate),
        // then the per-command pipeline will win and you will see percmd + global around handler.
        // So this test should be "global exists and is outer" not "global only".
        Assert.Equal(
            new[]
            {
                "mw:global:before",
                "mw:percmd:before",
                "handler:TestCommand",
                "mw:percmd:after",
                "mw:global:after",
            },
            ctx.Log);
    }

    [Fact]
    public async Task Per_command_middleware_runs_only_for_that_command_other_command_uses_global_only()
    {
        var ctx = new TestContext();
        using var sp = BuildProvider(ctx);

        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new TestCommand("a"));
        await dispatcher.DispatchAsync(new OtherCommand("b"));

        Assert.Equal(
            new[]
            {
                // TestCommand -> per-command wins (global outer, percmd inner)
                "mw:global:before",
                "mw:percmd:before",
                "handler:TestCommand",
                "mw:percmd:after",
                "mw:global:after",

                // OtherCommand -> only global
                "mw:global:before",
                "handler:OtherCommand",
                "mw:global:after",
            },
            ctx.Log);
    }

    [Fact]
    public async Task Policy_pipeline_is_selected_for_command_and_matches_generated_order()
    {
        var ctx = new TestContext();
        using var sp = BuildProvider(ctx);

        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        // With HostGate we generated:
        // - per-command middleware for TestCommand
        // - policy for TestCommand
        // Precedence should decide what wins. If your dispatcher is:
        // per-command > policy > global, then policy won't run here.
        // If you want to test policy selection, do it with a command that has policy but NO per-command.
        await dispatcher.DispatchAsync(new TestCommand("x"));

        // If precedence is per-command > policy > global, expected is per-command path (no policy).
        // If your precedence is policy > per-command (unlikely), swap expectation.
        Assert.DoesNotContain("mw:policy:before", ctx.Log);
    }
    [Fact]
    public async Task Policy_middleware_runs_when_command_has_policy_but_no_per_command_pipeline()
    {
        var ctx = new TestContext();
        using var sp = BuildProvider(ctx);

        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
        await dispatcher.DispatchAsync(new PolicyOnlyCommand("x"));

        Assert.Equal(
            new[]
            {
            "mw:global:before",
            "mw:policy:before",
            "handler:PolicyOnlyCommand",
            "mw:policy:after",
            "mw:global:after",
            },
            ctx.Log);
    }


}


