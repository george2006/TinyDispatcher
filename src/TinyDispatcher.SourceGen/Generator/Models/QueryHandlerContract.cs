using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Generator.Models;
public sealed record QueryHandlerContract(
      string QueryTypeFqn,
      string ResultTypeFqn,
      string HandlerTypeFqn)
{
    public Location HandlerLocation { get; init; } = Location.None;

    public QueryHandlerContract(
        string queryTypeFqn,
        string resultTypeFqn,
        string handlerTypeFqn,
        Location handlerLocation)
        : this(queryTypeFqn, resultTypeFqn, handlerTypeFqn)
    {
        HandlerLocation = handlerLocation;
    }
}
