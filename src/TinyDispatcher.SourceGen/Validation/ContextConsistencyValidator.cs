using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Validation;

internal static class ContextConsistencyValidator
{
    public static ImmutableArray<Diagnostic> Validate(
        DiagnosticsCatalog diags,
        ImmutableArray<UseTinyDispatcherCall> calls,
        bool contextIsRequired)
    {
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        if (calls.IsDefaultOrEmpty || calls.Length == 0)
        {
            if (contextIsRequired)
                return ImmutableArray.Create(diags.Create(diags.ContextTypeNotFound));

            return ImmutableArray<Diagnostic>.Empty;
        }

        // Hard rule: only one UseTinyDispatcher<TContext> call allowed
        if (calls.Length > 1)
        {
            var loc = calls[1].Location ?? Location.None;

            var contexts = string.Join(", ", calls.Select(c => c.ContextTypeFqn).Distinct());

            return ImmutableArray.Create(
                diags.Create(diags.MultipleContextsDetected, loc, contexts));
        }

        return ImmutableArray<Diagnostic>.Empty;
    }
}
