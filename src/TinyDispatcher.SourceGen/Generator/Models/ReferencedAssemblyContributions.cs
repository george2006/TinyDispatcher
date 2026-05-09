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
            {
                var command = assembly.Commands[j];
                if (CommandMatchesContext(command, expectedContextFqn))
                    yield return command;
            }
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

    private static bool CommandMatchesContext(
        HandlerContract command,
        string expectedContextFqn)
    {
        if (string.IsNullOrWhiteSpace(expectedContextFqn))
            return true;

        return string.Equals(
            command.ContextTypeFqn,
            expectedContextFqn,
            System.StringComparison.Ordinal);
    }
}
