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
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (diags is null)
        {
            throw new ArgumentNullException(nameof(diags));
        }

        if (!ShouldValidate(context))
        {
            return;
        }

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

        RememberThisAssemblyPerCommandMiddleware(context.ThisAssemblyPipeline, ownersByCommand);

        foreach (var referencedAssembly in context.ReferencedContributions.EnumerateMatchingContext(context.ContextTypeFqn))
        {
            RememberReferencedPerCommandMiddleware(context, diags, ownersByCommand, referencedAssembly);
        }
    }

    private static void RememberThisAssemblyPerCommandMiddleware(
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
        ReferencedAssemblyContribution referencedAssembly)
    {
        var reportedCommands = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < referencedAssembly.PerCommandMiddlewareContributions.Length; i++)
        {
            var contribution = referencedAssembly.PerCommandMiddlewareContributions[i];
            if (!ContextMatching.Matches(
                contribution.ContextTypeFqn,
                context.ContextTypeFqn))
            {
                continue;
            }

            var isFirstContributionForCommand = reportedCommands.Add(contribution.CommandTypeFqn);

            if (!isFirstContributionForCommand)
            {
                ReportPerCommandMiddlewareConflict(
                    context,
                    diags,
                    contribution.CommandTypeFqn,
                    referencedAssembly.AssemblyName,
                    referencedAssembly.AssemblyName);
                continue;
            }

            if (TryGetExistingOwner(ownersByCommand, contribution.CommandTypeFqn, out var existingOwner))
            {
                ReportPerCommandMiddlewareConflict(
                    context,
                    diags,
                    contribution.CommandTypeFqn,
                    existingOwner,
                    referencedAssembly.AssemblyName);
                continue;
            }

            ownersByCommand[contribution.CommandTypeFqn] = referencedAssembly.AssemblyName;
        }
    }

    private static void ValidatePolicyConflicts(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var ownersByPolicy = new Dictionary<string, string>(StringComparer.Ordinal);

        RememberThisAssemblyPolicies(context.ThisAssemblyPipeline, ownersByPolicy);

        foreach (var referencedAssembly in context.ReferencedContributions.EnumerateMatchingContext(context.ContextTypeFqn))
        {
            RememberReferencedPolicies(context, diags, ownersByPolicy, referencedAssembly);
        }
    }

    private static void RememberThisAssemblyPolicies(
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
        ReferencedAssemblyContribution referencedAssembly)
    {
        var reportedPolicies = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < referencedAssembly.PolicyContributions.Length; i++)
        {
            var contribution = referencedAssembly.PolicyContributions[i];
            if (!ContextMatching.Matches(
                contribution.ContextTypeFqn,
                context.ContextTypeFqn))
            {
                continue;
            }

            var isFirstContributionForPolicy = reportedPolicies.Add(contribution.PolicyTypeFqn);

            if (!isFirstContributionForPolicy)
            {
                ReportPolicyConflict(
                    context,
                    diags,
                    contribution.PolicyTypeFqn,
                    referencedAssembly.AssemblyName,
                    referencedAssembly.AssemblyName);
                continue;
            }

            if (TryGetExistingOwner(ownersByPolicy, contribution.PolicyTypeFqn, out var existingOwner))
            {
                ReportPolicyConflict(
                    context,
                    diags,
                    contribution.PolicyTypeFqn,
                    existingOwner,
                    referencedAssembly.AssemblyName);
                continue;
            }

            ownersByPolicy[contribution.PolicyTypeFqn] = referencedAssembly.AssemblyName;
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
        if (string.Equals(firstOwner, secondOwner, StringComparison.Ordinal))
        {
            return "'" + firstOwner + "'";
        }

        return string.Concat("'", firstOwner, "' and '", secondOwner, "'");
    }
}
