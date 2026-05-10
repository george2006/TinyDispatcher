using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContribution(
    string AssemblyName,
    string? ContextTypeFqn,
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableArray<ReferencedPerCommandMiddlewareContribution> PerCommandMiddlewareContributions,
    ImmutableArray<ReferencedPolicyContribution> PolicyContributions,
    ImmutableArray<ReferencedHandlerContribution> Handlers = default)
{
    public bool MatchesContext(string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(ContextTypeFqn) || string.IsNullOrWhiteSpace(contextFqn))
            return true;

        return string.Equals(ContextTypeFqn, contextFqn, System.StringComparison.Ordinal);
    }

    public bool HasContributions()
        => !string.IsNullOrWhiteSpace(ContextTypeFqn) ||
           !Globals.IsDefaultOrEmpty ||
           !PerCommandMiddlewareContributions.IsDefaultOrEmpty ||
           !PolicyContributions.IsDefaultOrEmpty ||
           !Handlers.IsDefaultOrEmpty;
}
