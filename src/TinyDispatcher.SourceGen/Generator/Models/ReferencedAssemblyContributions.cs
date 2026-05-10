using System.Collections.Generic;
using System.Collections.Immutable;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedAssemblyContributions(
    ImmutableArray<ReferencedAssemblyContribution> Assemblies)
{
    public static ReferencedAssemblyContributions Empty { get; } =
        new(ImmutableArray<ReferencedAssemblyContribution>.Empty);

    public bool HasCommands()
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            if (!Assemblies[i].Handlers.IsDefaultOrEmpty)
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerable<HandlerContract> EnumerateCommands(string contextFqn)
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            var assembly = Assemblies[i];
            if (!assembly.MatchesContext(contextFqn))
                continue;

            for (var j = 0; j < assembly.Handlers.Length; j++)
            {
                var handlerContribution = assembly.Handlers[j];
                if (!handlerContribution.MatchesContext(contextFqn))
                    continue;

                if (HandlerContractMatchesContext(handlerContribution.Handler, contextFqn))
                    yield return handlerContribution.Handler;
            }
        }
    }

    public IEnumerable<ReferencedAssemblyContribution> EnumerateMatchingContext(string contextFqn)
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            var assembly = Assemblies[i];
            if (assembly.MatchesContext(contextFqn))
                yield return assembly;
        }
    }

    private static bool HandlerContractMatchesContext(
        HandlerContract handlerContract,
        string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(contextFqn))
            return true;

        return string.Equals(
            handlerContract.ContextTypeFqn,
            contextFqn,
            System.StringComparison.Ordinal);
    }
}
