#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelineContributionsTests
{
    [Fact]
    public void Create_normalizes_global_and_per_command_middlewares_once()
    {
        var global = Middleware("MyApp.Middleware.GlobalMiddleware");
        var perCommand = Middleware("MyApp.Middleware.PerCommandMiddleware");

        var contributions = PipelineContributions.Create(
            ImmutableArray.Create(global),
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                "MyApp.Commands.Ping",
                ImmutableArray.Create(perCommand)),
            ImmutableDictionary<string, PolicySpec>.Empty);

        Assert.Equal("global::MyApp.Middleware.GlobalMiddleware", contributions.Globals[0].OpenTypeFqn);
        Assert.True(contributions.PerCommand.ContainsKey("global::MyApp.Commands.Ping"));
        Assert.Equal(
            "global::MyApp.Middleware.PerCommandMiddleware",
            contributions.PerCommand["global::MyApp.Commands.Ping"][0].OpenTypeFqn);
    }

    [Fact]
    public void Create_normalizes_policies_in_stable_order_and_skips_empty_middleware_policies()
    {
        var policies = ImmutableDictionary<string, PolicySpec>.Empty
            .Add(
                "global::MyApp.Policies.ZuluPolicy",
                new PolicySpec(
                    PolicyTypeFqn: "MyApp.Policies.ZuluPolicy",
                    Middlewares: ImmutableArray.Create(Middleware("MyApp.Middleware.ZuluMiddleware")),
                    Commands: ImmutableArray.Create("MyApp.Commands.Checkout")))
            .Add(
                "global::MyApp.Policies.AlphaPolicy",
                new PolicySpec(
                    PolicyTypeFqn: "MyApp.Policies.AlphaPolicy",
                    Middlewares: ImmutableArray.Create(Middleware("MyApp.Middleware.AlphaMiddleware")),
                    Commands: ImmutableArray.Create("MyApp.Commands.Checkout", " ")))
            .Add(
                "global::MyApp.Policies.EmptyPolicy",
                new PolicySpec(
                    PolicyTypeFqn: "MyApp.Policies.EmptyPolicy",
                    Middlewares: ImmutableArray<MiddlewareRef>.Empty,
                    Commands: ImmutableArray.Create("MyApp.Commands.Checkout")));

        var contributions = PipelineContributions.Create(
            ImmutableArray<MiddlewareRef>.Empty,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            policies);

        Assert.Equal(2, contributions.Policies.Length);

        Assert.Equal("global::MyApp.Policies.AlphaPolicy", contributions.Policies[0].PolicyTypeFqn);
        Assert.Equal("global::MyApp.Middleware.AlphaMiddleware", contributions.Policies[0].Middlewares[0].OpenTypeFqn);
        Assert.Equal(
            new[] { "global::MyApp.Commands.Checkout" },
            contributions.Policies[0].Commands);

        Assert.Equal("global::MyApp.Policies.ZuluPolicy", contributions.Policies[1].PolicyTypeFqn);

        Assert.True(contributions.PolicyByCommand.TryGetValue(
            "global::MyApp.Commands.Checkout",
            out var winningPolicy));
        Assert.Equal("global::MyApp.Policies.AlphaPolicy", winningPolicy.PolicyTypeFqn);
    }

    private static MiddlewareRef Middleware(string openTypeFqn)
    {
        return new MiddlewareRef(
            OpenTypeSymbol: default!,
            OpenTypeFqn: openTypeFqn,
            Arity: 2);
    }
}

