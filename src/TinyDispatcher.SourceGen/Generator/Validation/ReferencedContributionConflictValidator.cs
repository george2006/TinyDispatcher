#nullable enable

using System;
using System.Collections.Generic;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class ReferencedContributionConflictValidator : IGeneratorValidator
{
    private const string LocalOwner = "this project";

    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        if (!ShouldValidate(context))
            return;

        ValidatePerCommandMiddlewareConflicts(context, diags);
        ValidatePolicyConflicts(context, diags);
    }

    private static bool ShouldValidate(GeneratorValidationContext context)
    {
        return context.IsHostProject &&
               !string.IsNullOrWhiteSpace(context.ExpectedContextFqn);
    }

    private static void ValidatePerCommandMiddlewareConflicts(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var ownersByCommand = new Dictionary<string, string>(StringComparer.Ordinal);

        RememberLocalPerCommandMiddleware(context.LocalPipeline, ownersByCommand);

        foreach (var assembly in context.ReferencedContributions.EnumerateMatchingContext(context.ExpectedContextFqn))
        {
            RememberReferencedPerCommandMiddleware(context, diags, ownersByCommand, assembly);
        }
    }

    private static void RememberLocalPerCommandMiddleware(
        PipelineConfig pipeline,
        Dictionary<string, string> ownersByCommand)
    {
        foreach (var pair in pipeline.PerCommand)
        {
            ownersByCommand[pair.Key] = LocalOwner;
        }
    }

    private static void RememberReferencedPerCommandMiddleware(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        Dictionary<string, string> ownersByCommand,
        ReferencedAssemblyContribution assembly)
    {
        foreach (var pair in assembly.PerCommand)
        {
            if (TryGetExistingOwner(ownersByCommand, pair.Key, out var existingOwner))
            {
                ReportPerCommandMiddlewareConflict(context, diags, pair.Key, existingOwner, assembly.AssemblyName);
                continue;
            }

            ownersByCommand[pair.Key] = assembly.AssemblyName;
        }
    }

    private static void ValidatePolicyConflicts(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var ownersByPolicy = new Dictionary<string, string>(StringComparer.Ordinal);

        RememberLocalPolicies(context.LocalPipeline, ownersByPolicy);

        foreach (var assembly in context.ReferencedContributions.EnumerateMatchingContext(context.ExpectedContextFqn))
        {
            RememberReferencedPolicies(context, diags, ownersByPolicy, assembly);
        }
    }

    private static void RememberLocalPolicies(
        PipelineConfig pipeline,
        Dictionary<string, string> ownersByPolicy)
    {
        foreach (var pair in pipeline.Policies)
        {
            ownersByPolicy[pair.Key] = LocalOwner;
        }
    }

    private static void RememberReferencedPolicies(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        Dictionary<string, string> ownersByPolicy,
        ReferencedAssemblyContribution assembly)
    {
        foreach (var pair in assembly.Policies)
        {
            if (TryGetExistingOwner(ownersByPolicy, pair.Key, out var existingOwner))
            {
                ReportPolicyConflict(context, diags, pair.Key, existingOwner, assembly.AssemblyName);
                continue;
            }

            ownersByPolicy[pair.Key] = assembly.AssemblyName;
        }
    }

    private static bool TryGetExistingOwner(
        Dictionary<string, string> owners,
        string key,
        out string owner)
    {
        return owners.TryGetValue(key, out owner!);
    }

    private static void ReportPerCommandMiddlewareConflict(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        string commandTypeFqn,
        string firstOwner,
        string secondOwner)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.DuplicatePerCommandMiddlewareContribution,
            commandTypeFqn,
            JoinOwners(firstOwner, secondOwner)));
    }

    private static void ReportPolicyConflict(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        string policyTypeFqn,
        string firstOwner,
        string secondOwner)
    {
        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.DuplicatePolicyContribution,
            policyTypeFqn,
            JoinOwners(firstOwner, secondOwner)));
    }

    private static string JoinOwners(string firstOwner, string secondOwner)
    {
        return string.Concat("'", firstOwner, "' and '", secondOwner, "'");
    }
}
