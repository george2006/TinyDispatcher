#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelinePerCommandMiddlewareMapTests
{
    [Fact]
    public void Build_normalizes_commands_and_skips_empty_entries()
    {
        var middleware = new MiddlewareRef(
            OpenTypeSymbol: default!,
            OpenTypeFqn: "MyApp.Middleware.LoggingMiddleware",
            Arity: 2);

        var map = PipelinePerCommandMiddlewareMap.Build(
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty
                .Add("MyApp.Commands.Ping", ImmutableArray.Create(middleware))
                .Add(" ", ImmutableArray.Create(middleware))
                .Add("MyApp.Commands.Empty", ImmutableArray<MiddlewareRef>.Empty));

        Assert.Single(map);
        Assert.True(map.ContainsKey("global::MyApp.Commands.Ping"));
        Assert.Equal("global::MyApp.Middleware.LoggingMiddleware", map["global::MyApp.Commands.Ping"][0].OpenTypeFqn);
    }
}

