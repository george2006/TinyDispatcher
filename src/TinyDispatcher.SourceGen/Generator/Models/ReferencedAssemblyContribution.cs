using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContribution(
    string AssemblyName,
    string? ContextTypeFqn,
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableArray<ReferencedPerCommandMiddlewareContribution> PerCommandMiddlewareContributions,
    ImmutableArray<ReferencedPolicyContribution> PolicyContributions,
    ImmutableArray<HandlerContract> Handlers = default)
{
    public bool HasContributions()
        => !string.IsNullOrWhiteSpace(ContextTypeFqn) ||
           !Globals.IsDefaultOrEmpty ||
           !PerCommandMiddlewareContributions.IsDefaultOrEmpty ||
           !PolicyContributions.IsDefaultOrEmpty ||
           !Handlers.IsDefaultOrEmpty;
}
