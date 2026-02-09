#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen;

public sealed class DuplicateHandlerValidator : IValidator
{
    private readonly DiagnosticsCatalog _diags;

    public DuplicateHandlerValidator(DiagnosticsCatalog diags)
        => _diags = diags ?? throw new ArgumentNullException(nameof(diags));

    public ImmutableArray<Diagnostic> Validate(DiscoveryResult result)
    {
        var builder = ImmutableArray.CreateBuilder<Diagnostic>();

        // Commands: duplicate by MessageTypeFqn
        foreach (var g in result.Commands.GroupBy(x => x.MessageTypeFqn))
        {
            if (g.Count() <= 1) continue;

            var first = g.ElementAt(0).HandlerTypeFqn;
            var second = g.ElementAt(1).HandlerTypeFqn;

            builder.Add(_diags.Create(_diags.DuplicateCommand, g.Key, first, second));
        }

        // Queries: duplicate by QueryTypeFqn (result type is already part of the contract, but query type is the key)
        foreach (var g in result.Queries.GroupBy(x => x.QueryTypeFqn))
        {
            if (g.Count() <= 1) continue;

            var first = g.ElementAt(0).HandlerTypeFqn;
            var second = g.ElementAt(1).HandlerTypeFqn;

            builder.Add(_diags.Create(_diags.DuplicateQuery, g.Key, first, second));
        }

        return builder.ToImmutable();
    }
}
