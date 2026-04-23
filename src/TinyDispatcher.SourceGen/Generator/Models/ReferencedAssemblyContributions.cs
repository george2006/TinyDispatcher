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
            if (!Assemblies[i].Commands.IsDefaultOrEmpty)
                return true;
        }

        return false;
    }

    public IEnumerable<HandlerContract> EnumerateCommands(string expectedContextFqn)
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            var assembly = Assemblies[i];
            if (!assembly.MatchesContext(expectedContextFqn))
                continue;

            for (var j = 0; j < assembly.Commands.Length; j++)
                yield return assembly.Commands[j];
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
}
