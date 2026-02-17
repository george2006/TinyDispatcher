using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

sealed record DiscoveryResult(
       ImmutableArray<HandlerContract> Commands,
       ImmutableArray<QueryHandlerContract> Queries);
