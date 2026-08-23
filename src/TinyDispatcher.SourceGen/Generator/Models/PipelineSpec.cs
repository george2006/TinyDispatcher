using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Models;

public sealed record PolicySpec(
        string PolicyTypeFqn,
        ImmutableArray<MiddlewareRef> Middlewares,
        ImmutableArray<string> Commands
    )
{
    public Location PolicyLocation { get; init; } = Location.None;

    public PolicySpec(
        string policyTypeFqn,
        ImmutableArray<MiddlewareRef> middlewares,
        ImmutableArray<string> commands,
        Location policyLocation)
        : this(policyTypeFqn, middlewares, commands)
    {
        PolicyLocation = policyLocation;
    }
}
