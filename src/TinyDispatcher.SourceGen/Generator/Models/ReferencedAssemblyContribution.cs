using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContribution(
    string AssemblyName,
    string? ContextTypeFqn,
    ImmutableArray<HandlerContract> Commands,
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableArray<PerCommandMiddlewareFinding> PerCommandMiddlewareFindings,
    ImmutableArray<PolicyFinding> PolicyFindings)
{
    public bool MatchesContext(string expectedContextFqn)
    {
        if (string.IsNullOrWhiteSpace(ContextTypeFqn) || string.IsNullOrWhiteSpace(expectedContextFqn))
            return true;

        return string.Equals(ContextTypeFqn, expectedContextFqn, System.StringComparison.Ordinal);
    }

    public bool HasContributions()
        => !string.IsNullOrWhiteSpace(ContextTypeFqn) ||
           !Commands.IsDefaultOrEmpty ||
           !Globals.IsDefaultOrEmpty ||
           !PerCommandMiddlewareFindings.IsDefaultOrEmpty ||
           !PolicyFindings.IsDefaultOrEmpty;
}
