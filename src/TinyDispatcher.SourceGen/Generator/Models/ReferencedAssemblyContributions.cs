using System.Collections.Generic;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContributions(
    ImmutableArray<ReferencedAssemblyContribution> Assemblies,
    ImmutableArray<ReferencedHandlerContribution> Handlers)
{
    public static ReferencedAssemblyContributions Empty { get; } =
        new(
            ImmutableArray<ReferencedAssemblyContribution>.Empty,
            ImmutableArray<ReferencedHandlerContribution>.Empty);

    public ReferencedAssemblyContributions(ImmutableArray<ReferencedAssemblyContribution> assemblies)
        : this(assemblies, ImmutableArray<ReferencedHandlerContribution>.Empty)
    {
    }

    public bool HasCommands()
    {
        return !Handlers.IsDefaultOrEmpty;
    }

    public IEnumerable<HandlerContract> EnumerateCommands(string expectedContextFqn)
    {
        for (var i = 0; i < Handlers.Length; i++)
        {
            var handlerContribution = Handlers[i];
            if (!handlerContribution.MatchesContext(expectedContextFqn))
                continue;

            if (HandlerContractMatchesContext(handlerContribution.Handler, expectedContextFqn))
                yield return handlerContribution.Handler;
        }
    }

    public IEnumerable<ReferencedAssemblyContribution> EnumerateMatchingContext(string expectedContextFqn)
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            var assembly = Assemblies[i];
            if (assembly.MatchesContext(expectedContextFqn))
                yield return assembly;
        }
    }

    private static bool HandlerContractMatchesContext(
        HandlerContract handlerContract,
        string expectedContextFqn)
    {
        if (string.IsNullOrWhiteSpace(expectedContextFqn))
            return true;

        return string.Equals(
            handlerContract.ContextTypeFqn,
            expectedContextFqn,
            System.StringComparison.Ordinal);
    }
}
