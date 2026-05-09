using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;

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

        // Multiple bootstrap calls are allowed when they target the same context.
        // Different contexts still need generation support before we can accept them.
        var hasDifferentContext = TryFindFirstDifferentContextCall(calls, out var differentContextCall);

        if (hasDifferentContext)
        {
            var loc = differentContextCall.Location ?? Location.None;
            var contexts = GetDistinctContexts(calls);

            diags.Add(catalog.Create(catalog.MultipleContextsDetected, loc, contexts));
        }
    }

    private static bool TryFindFirstDifferentContextCall(
        ImmutableArray<UseTinyDispatcherCall> calls,
        out UseTinyDispatcherCall differentContextCall)
    {
        differentContextCall = default;

        if (calls.Length <= 1)
        {
            return false;
        }

        var firstContext = calls[0].ContextTypeFqn;

        for (var i = 1; i < calls.Length; i++)
        {
            var isDifferentContext = TargetsDifferentContext(calls[i], firstContext);

            if (isDifferentContext)
            {
                differentContextCall = calls[i];
                return true;
            }
        }

        return false;
    }

    private static bool TargetsDifferentContext(
        UseTinyDispatcherCall call,
        string expectedContextFqn)
    {
        return !string.Equals(
            expectedContextFqn,
            call.ContextTypeFqn,
            StringComparison.Ordinal);
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
