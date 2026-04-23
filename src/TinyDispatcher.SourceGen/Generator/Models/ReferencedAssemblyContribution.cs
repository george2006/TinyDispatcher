using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContribution(
    string AssemblyName,
    string? ContextTypeFqn,
    ImmutableArray<HandlerContract> Commands,
    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies)
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
           PerCommand.Count > 0 ||
           Policies.Count > 0;
}
