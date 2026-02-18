using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class ContextConsistencyValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        // If this is not a host project (no UseTinyDispatcher(...) bootstrap calls),
        // then pipelines won't be emitted, so a context is not required.
        if (!context.IsHostProject)
            return;

        var calls = context.UseTinyDispatcherCalls;
        var catalog = context.Diagnostics;

        // No UseTinyDispatcher<TContext> call found (but required for codegen)
        if (calls.IsDefaultOrEmpty || calls.Length == 0)
        {
            diags.Add(catalog.Create(catalog.ContextTypeNotFound));
            return;
        }

        // Hard rule: only one UseTinyDispatcher<TContext> call allowed per project
        if (calls.Length > 1)
        {
            var loc = calls[1].Location ?? Location.None;

            var contexts = string.Join(", ",
                calls.Select(c => c.ContextTypeFqn).Distinct());

            diags.Add(catalog.Create(catalog.MultipleContextsDetected, loc, contexts));
        }
    }
}
