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
            if (!ContextMatching.Matches(assembly.ContextTypeFqn, contextFqn))
            {
                continue;
            }

            for (var j = 0; j < assembly.Handlers.Length; j++)
            {
                var handler = assembly.Handlers[j];
                if (ContextMatching.Matches(handler.ContextTypeFqn, contextFqn))
                {
                    yield return handler;
                }
            }
        }
    }

    public IEnumerable<ReferencedAssemblyContribution> EnumerateMatchingContext(string contextFqn)
    {
        for (var i = 0; i < Assemblies.Length; i++)
        {
            var assembly = Assemblies[i];
            if (ContextMatching.Matches(assembly.ContextTypeFqn, contextFqn))
            {
                yield return assembly;
            }
        }
    }

}
