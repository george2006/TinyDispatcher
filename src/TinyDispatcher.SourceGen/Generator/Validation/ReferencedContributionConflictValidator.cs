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
               !string.IsNullOrWhiteSpace(context.ContextTypeFqn);
    }

    private static void ValidatePerCommandMiddlewareConflicts(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var ownersByCommand = new Dictionary<string, string>(StringComparer.Ordinal);

        RememberLocalPerCommandMiddleware(context.LocalPipeline, ownersByCommand);

        foreach (var assembly in context.ReferencedContributions.EnumerateMatchingContext(context.ContextTypeFqn))
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
        var reportedCommands = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < assembly.PerCommandMiddlewareFindings.Length; i++)
        {
            var finding = assembly.PerCommandMiddlewareFindings[i];
            if (ContributionBelongsToAnotherContext(
                finding.ContextTypeFqn,
                context.ContextTypeFqn))
            {
                continue;
            }

            var isFirstContributionForCommand = reportedCommands.Add(finding.CommandTypeFqn);

            if (!isFirstContributionForCommand)
            {
                ReportPerCommandMiddlewareConflict(
                    context,
                    diags,
                    finding.CommandTypeFqn,
                    assembly.AssemblyName,
                    assembly.AssemblyName);
                continue;
            }

            if (TryGetExistingOwner(ownersByCommand, finding.CommandTypeFqn, out var existingOwner))
            {
                ReportPerCommandMiddlewareConflict(
                    context,
                    diags,
                    finding.CommandTypeFqn,
                    existingOwner,
                    assembly.AssemblyName);
                continue;
            }

            ownersByCommand[finding.CommandTypeFqn] = assembly.AssemblyName;
        }
    }

    private static void ValidatePolicyConflicts(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var ownersByPolicy = new Dictionary<string, string>(StringComparer.Ordinal);

        RememberLocalPolicies(context.LocalPipeline, ownersByPolicy);

        foreach (var assembly in context.ReferencedContributions.EnumerateMatchingContext(context.ContextTypeFqn))
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
        var reportedPolicies = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < assembly.PolicyFindings.Length; i++)
        {
            var finding = assembly.PolicyFindings[i];
            if (ContributionBelongsToAnotherContext(
                finding.ContextTypeFqn,
                context.ContextTypeFqn))
            {
                continue;
            }

            var isFirstContributionForPolicy = reportedPolicies.Add(finding.PolicyTypeFqn);

            if (!isFirstContributionForPolicy)
            {
                ReportPolicyConflict(
                    context,
                    diags,
                    finding.PolicyTypeFqn,
                    assembly.AssemblyName,
                    assembly.AssemblyName);
                continue;
            }

            if (TryGetExistingOwner(ownersByPolicy, finding.PolicyTypeFqn, out var existingOwner))
            {
                ReportPolicyConflict(
                    context,
                    diags,
                    finding.PolicyTypeFqn,
                    existingOwner,
                    assembly.AssemblyName);
                continue;
            }

            ownersByPolicy[finding.PolicyTypeFqn] = assembly.AssemblyName;
        }
    }

    private static bool TryGetExistingOwner(
        Dictionary<string, string> owners,
        string key,
        out string owner)
    {
        return owners.TryGetValue(key, out owner!);
    }

    private static bool ContributionBelongsToAnotherContext(
        string? contributionContextFqn,
        string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(contributionContextFqn) ||
            string.IsNullOrWhiteSpace(contextFqn))
        {
            return false;
        }

        return !string.Equals(
            contributionContextFqn,
            contextFqn,
            StringComparison.Ordinal);
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
        if (string.Equals(firstOwner, secondOwner, StringComparison.Ordinal))
            return "'" + firstOwner + "'";

        return string.Concat("'", firstOwner, "' and '", secondOwner, "'");
    }
}
