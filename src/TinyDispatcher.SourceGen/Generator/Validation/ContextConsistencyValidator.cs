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

        var hasRepeatedContext = TryFindFirstRepeatedContextCall(calls, out var repeatedContextCall);

        if (hasRepeatedContext)
        {
            var loc = repeatedContextCall.Location ?? Location.None;
            diags.Add(catalog.Create(
                catalog.RepeatedContextBootstrap,
                loc,
                repeatedContextCall.ContextTypeFqn));
        }
    }

    private static bool TryFindFirstRepeatedContextCall(
        ImmutableArray<UseTinyDispatcherCall> calls,
        out UseTinyDispatcherCall repeatedContextCall)
    {
        repeatedContextCall = default;

        if (calls.Length <= 1)
        {
            return false;
        }

        var seenContexts = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < calls.Length; i++)
        {
            var call = calls[i];
            var isFirstCallForContext = seenContexts.Add(call.ContextTypeFqn);

            if (!isFirstCallForContext)
            {
                repeatedContextCall = call;
                return true;
            }
        }

        return false;
    }
}
