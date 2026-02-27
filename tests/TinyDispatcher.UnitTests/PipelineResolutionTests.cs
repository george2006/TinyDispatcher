using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;
using TinyDispatcher.UnitTests;
using Xunit;
using static TinyDispatcher.UnitTests.ConcurrentDispatchSameCommandTypeTests;
using static TinyDispatcher.UnitTets.PipelineSelectionTests;

namespace TinyDispatcher.UnitTets;

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
        services.AddSingleton<ProbeState>();
        services.AddScoped(typeof(GlobalLogMiddleware<,>));
        services.AddScoped(typeof(PerCommandLogMiddleware<,>));
        services.AddScoped(typeof(PolicyLogMiddleware<,>));
        services.AddScoped<ICommandHandler<TestCommand, TestContext>, TestHandler>();
        services.AddSingleton(typeof(CallTracker));
        services.AddScoped<IContextFactory<TestContext>>(_ => new FixedContextFactory(ctx));

        TinyDispatcher.Generated.ThisAssemblyPipelineContribution.Add(services);

        services.AddScoped<IDispatcher<TestContext>>(sp =>
            new Dispatcher<TestContext>(sp, sp.GetRequiredService<IContextFactory<TestContext>>()));

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
                "mw:policy:before",
                "mw:percmd:before",
                "handler:TestCommand",
                "mw:percmd:after",
                "mw:policy:after",
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
            // TestCommand -> global outer, then policy, then percmd
            "mw:global:before",
            "mw:policy:before",
            "mw:percmd:before",
            "handler:TestCommand",
            "mw:percmd:after",
            "mw:policy:after",
            "mw:global:after",

            // OtherCommand -> only global (assuming no policy applies to OtherCommand)
            "mw:global:before",
            "handler:OtherCommand",
            "mw:global:after",
        },
        ctx.Log);
        }

    [Fact]
    public async Task Policy_middleware_runs_for_command_even_when_per_command_pipeline_exists_and_order_is_global_policy_percmd()
    {
        var ctx = new TestContext();
        using var sp = BuildProvider(ctx);

        var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();

        await dispatcher.DispatchAsync(new TestCommand("x"));

        Assert.Equal(
            new[]
            {
            "mw:global:before",
            "mw:policy:before",
            "mw:percmd:before",
            "handler:TestCommand",
            "mw:percmd:after",
            "mw:policy:after",
            "mw:global:after",
            },
            ctx.Log);
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


