using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

/// <summary>
/// Represents a single UseTinyDispatcher&lt;TContext&gt; invocation discovered in syntax.
/// We resolve the context type via semantic model when validating.
/// </summary>
internal readonly record struct UseTinyDispatcherCall(
    string ContextTypeFqn,
    Location Location);
