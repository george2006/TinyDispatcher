#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class DuplicateHandlerValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        var result = context.DiscoveryResult;
        var catalog = context.Diagnostics;

        // Commands: duplicate by MessageTypeFqn
        foreach (var g in result.Commands.GroupBy(x => x.MessageTypeFqn))
        {
            if (g.Count() <= 1) continue;

            var first = g.ElementAt(0).HandlerTypeFqn;
            var second = g.ElementAt(1).HandlerTypeFqn;

            diags.Add(catalog.Create(catalog.DuplicateCommand, g.Key, first, second));
        }

        // Queries: duplicate by QueryTypeFqn
        foreach (var g in result.Queries.GroupBy(x => x.QueryTypeFqn))
        {
            if (g.Count() <= 1) continue;

            var first = g.ElementAt(0).HandlerTypeFqn;
            var second = g.ElementAt(1).HandlerTypeFqn;

            diags.Add(catalog.Create(catalog.DuplicateQuery, g.Key, first, second));
        }
    }
}
