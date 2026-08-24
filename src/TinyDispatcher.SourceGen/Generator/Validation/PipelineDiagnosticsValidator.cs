using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class PipelineDiagnosticsValidator : IGeneratorValidator
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

        var discoveredCommands = BuildDiscoveredCommandSet(context.DiscoveryResult);

        ValidatePerCommandMiddlewareTargets(context, diags, discoveredCommands);
        ValidatePolicyTargets(context, diags, discoveredCommands);
        ValidateMultiplePoliciesPerCommand(context, diags);
    }

    private static HashSet<string> BuildDiscoveredCommandSet(DiscoveryResult discovery)
    {
        var discoveredCommands = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < discovery.Commands.Length; i++)
        {
            AddNormalizedCommand(discoveredCommands, discovery.Commands[i].MessageTypeFqn);
        }

        return discoveredCommands;
    }

    private static void ValidatePerCommandMiddlewareTargets(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        HashSet<string> discoveredCommands)
    {
        if (context.PerCommand.Count == 0)
        {
            return;
        }

        foreach (var perCommandPipeline in context.PerCommand)
        {
            var hasNormalizedCommand = TryNormalizeCommand(perCommandPipeline.Key, out var command);
            if (!hasNormalizedCommand)
            {
                continue;
            }

            var commandWasDiscovered = discoveredCommands.Contains(command);
            if (!commandWasDiscovered)
            {
                diags.Add(context.Diagnostics.Create(
                    context.Diagnostics.MiddlewareConfiguredForUnknownCommand,
                    GetFirstMiddlewareLocation(perCommandPipeline.Value),
                    command));
            }
        }
    }

    private static Location GetFirstMiddlewareLocation(IReadOnlyList<MiddlewareRef> middlewares)
    {
        return middlewares.Count > 0 ? middlewares[0].MiddlewareLocation : Location.None;
    }

    private static void ValidatePolicyTargets(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        HashSet<string> discoveredCommands)
    {
        if (context.Policies.Count == 0)
        {
            return;
        }

        foreach (var policy in context.Policies.Values)
        {
            var policyType = PipelineTypeNames.NormalizeFqn(policy.PolicyTypeFqn);

            for (var i = 0; i < policy.Commands.Length; i++)
            {
                var hasNormalizedCommand = TryNormalizeCommand(policy.Commands[i], out var command);
                if (!hasNormalizedCommand)
                {
                    continue;
                }

                var commandWasDiscovered = discoveredCommands.Contains(command);
                if (!commandWasDiscovered)
                {
                    diags.Add(context.Diagnostics.Create(
                        context.Diagnostics.PolicyTargetsUnknownCommand,
                        policy.PolicyLocation,
                        policyType,
                        command));
                }
            }
        }
    }

    private static void ValidateMultiplePoliciesPerCommand(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        if (context.Policies.Count == 0)
        {
            return;
        }

        var policiesByCommand = BuildPoliciesByCommand(context.Policies.Values);

        foreach (var policyGroup in policiesByCommand)
        {
            if (policyGroup.Value.Count <= 1)
            {
                continue;
            }

            var policyNames = JoinDistinct(policyGroup.Value);
            var conflictingPolicyLocation = policyGroup.Value[1].PolicyLocation;

            diags.Add(context.Diagnostics.Create(
                context.Diagnostics.MultiplePoliciesForSameCommand,
                conflictingPolicyLocation,
                policyGroup.Key,
                policyNames));
        }
    }

    private static Dictionary<string, List<PolicySpec>> BuildPoliciesByCommand(IEnumerable<PolicySpec> policies)
    {
        var policiesByCommand = new Dictionary<string, List<PolicySpec>>(StringComparer.Ordinal);

        foreach (var policy in policies)
        {
            var policyType = PipelineTypeNames.NormalizeFqn(policy.PolicyTypeFqn);
            if (string.IsNullOrWhiteSpace(policyType))
            {
                continue;
            }

            AddPolicyCommands(policiesByCommand, policy.Commands, policy);
        }

        return policiesByCommand;
    }

    private static void AddPolicyCommands(
        Dictionary<string, List<PolicySpec>> policiesByCommand,
        IReadOnlyList<string> commands,
        PolicySpec policy)
    {
        for (var i = 0; i < commands.Count; i++)
        {
            if (!TryNormalizeCommand(commands[i], out var command))
            {
                continue;
            }

            if (!policiesByCommand.TryGetValue(command, out var policies))
            {
                policies = new List<PolicySpec>(4);
                policiesByCommand[command] = policies;
            }

            policies.Add(policy);
        }
    }

    private static void AddNormalizedCommand(HashSet<string> commands, string commandType)
    {
        if (TryNormalizeCommand(commandType, out var command))
        {
            commands.Add(command);
        }
    }

    private static bool TryNormalizeCommand(string commandType, out string command)
    {
        command = PipelineTypeNames.NormalizeFqn(commandType);
        return !string.IsNullOrWhiteSpace(command);
    }

    private static string JoinDistinct(List<PolicySpec> policies)
    {
        var distinct = new List<string>(policies.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < policies.Count; i++)
        {
            var value = PipelineTypeNames.NormalizeFqn(policies[i].PolicyTypeFqn);
            if (seen.Add(value))
            {
                distinct.Add(value);
            }
        }

        return string.Join(", ", distinct);
    }
}

