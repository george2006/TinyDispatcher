using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ContextValidationInput(
    string ContextTypeFqn,
    ImmutableArray<UseTinyDispatcherCall> BootstrapCalls,
    ContextGenerationInput GenerationInput);
