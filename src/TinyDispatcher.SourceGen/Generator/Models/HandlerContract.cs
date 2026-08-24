using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Models;
public sealed record HandlerContract(
        string MessageTypeFqn,
        string HandlerTypeFqn,
        string ContextTypeFqn)
{
    public Location HandlerLocation { get; init; } = Location.None;

    public HandlerContract(
        string messageTypeFqn,
        string handlerTypeFqn,
        string contextTypeFqn,
        Location handlerLocation)
        : this(messageTypeFqn, handlerTypeFqn, contextTypeFqn)
    {
        HandlerLocation = handlerLocation;
    }
}

