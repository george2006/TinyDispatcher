using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class ContextConsistencyValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (diags is null)
        {
            throw new ArgumentNullException(nameof(diags));
        }

        // If this is not a host project (no UseTinyDispatcher(...) bootstrap calls),
        // then pipelines won't be emitted, so a context is not required.
        var isLibraryProject = !context.IsHostProject;

        if (isLibraryProject)
        {
            return;
        }

        var calls = context.UseTinyDispatcherCalls;
        var catalog = context.Diagnostics;

        // No UseTinyDispatcher<TContext> call found (but required for codegen)
        var hasNoBootstrapCall = calls.IsDefaultOrEmpty || calls.Length == 0;

        if (hasNoBootstrapCall)
        {
            diags.Add(catalog.Create(catalog.ContextTypeNotFound));
            return;
        }

        // Hard rule: only one UseTinyDispatcher<TContext> call allowed per project
        var hasMultipleBootstrapCalls = calls.Length > 1;

        if (hasMultipleBootstrapCalls)
        {
            var loc = calls[1].Location ?? Location.None;
            var contexts = GetDistinctContexts(calls);

            diags.Add(catalog.Create(catalog.MultipleContextsDetected, loc, contexts));
        }
    }

    private static string GetDistinctContexts(ImmutableArray<UseTinyDispatcherCall> calls)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var contexts = new List<string>();

        for (var i = 0; i < calls.Length; i++)
        {
            var context = calls[i].ContextTypeFqn;
            var isFirstOccurrence = seen.Add(context);

            if (isFirstOccurrence)
            {
                contexts.Add(context);
            }
        }

        return string.Join(", ", contexts);
    }
}
