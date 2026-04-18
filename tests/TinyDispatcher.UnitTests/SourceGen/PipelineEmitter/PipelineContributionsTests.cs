#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
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

    private static MiddlewareRef Middleware(string openTypeFqn)
    {
        return new MiddlewareRef(
            OpenTypeSymbol: default!,
            OpenTypeFqn: openTypeFqn,
            Arity: 2);
    }
}
