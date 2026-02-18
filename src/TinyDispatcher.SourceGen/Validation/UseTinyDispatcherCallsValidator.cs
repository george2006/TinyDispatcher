#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TinyDispatcher.SourceGen.Validation;

/// <summary>
/// Validates UseTinyDispatcher&lt;TContext&gt; usage:
/// - If context is required, at least one call must exist (DISP111)
/// - Only one call is allowed per project (DISP110)
/// </summary>
internal sealed class UseTinyDispatcherCallsValidator : IGeneratorValidator
{
    private readonly bool _contextIsRequired;

    public UseTinyDispatcherCallsValidator(bool contextIsRequired)
    {
        _contextIsRequired = contextIsRequired;
    }

    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        var catalog = context.Diagnostics;
        var calls = context.UseTinyDispatcherCalls;

        // No calls found
        if (calls.IsDefaultOrEmpty || calls.Length == 0)
        {
            if (_contextIsRequired)
                diags.Add(catalog.Create(catalog.ContextTypeNotFound));

            return;
        }

        // Hard rule: only one UseTinyDispatcher<TContext> call allowed
        if (calls.Length > 1)
        {
            // Use the 2nd call location (same as old implementation)
            var loc = calls[1].Location ?? Location.None;

            var contexts = string.Join(", ",
                calls.Select(c => c.ContextTypeFqn).Distinct());

            diags.Add(catalog.Create(catalog.MultipleContextsDetected, loc, contexts));
        }
    }
}
