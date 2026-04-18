#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelinePolicyCommandMapTests
{
    [Fact]
    public void AddFirstPolicyWins_normalizes_commands_and_keeps_first_value()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        PipelinePolicyCommandMap.AddFirstPolicyWins(
            map,
            ImmutableArray.Create("MyApp.Commands.Ping", "global::MyApp.Commands.Pong", ""),
            "first");

        PipelinePolicyCommandMap.AddFirstPolicyWins(
            map,
            ImmutableArray.Create("global::MyApp.Commands.Ping"),
            "second");

        Assert.Equal("first", map["global::MyApp.Commands.Ping"]);
        Assert.Equal("first", map["global::MyApp.Commands.Pong"]);
        Assert.False(map.ContainsKey(string.Empty));
    }
}
