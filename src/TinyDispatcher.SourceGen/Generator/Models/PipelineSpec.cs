using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

public sealed record PolicySpec(
        string PolicyTypeFqn,
        ImmutableArray<MiddlewareRef> Middlewares,
        ImmutableArray<string> Commands
    );
