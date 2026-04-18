using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelineRegistrationPlanner
{
    public static ImmutableArray<ServiceRegistration> Build(
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        bool hasGlobal,
        DiscoveryResult discovery,
        IReadOnlyDictionary<string, MiddlewareRef[]> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var perCommandSet = new HashSet<string>(perCommand.Keys, StringComparer.Ordinal);
        var commandToPolicyPipeline = BuildCommandToPolicyPipelineNames(generatedNamespace, policies);
        var policyCommandSet = new HashSet<string>(commandToPolicyPipeline.Keys, StringComparer.Ordinal);
        var registrations = new List<ServiceRegistration>(256);

        AddPerCommandRegistrations(registrations, generatedNamespace, coreNamespace, contextTypeFqn, perCommandSet);
        AddPolicyRegistrations(registrations, coreNamespace, contextTypeFqn, perCommandSet, policyCommandSet, commandToPolicyPipeline);
        AddGlobalRegistrations(registrations, generatedNamespace, coreNamespace, contextTypeFqn, hasGlobal, discovery, perCommandSet, policyCommandSet);

        return registrations.ToImmutableArray();
    }

    private static Dictionary<string, string> BuildCommandToPolicyPipelineNames(
        string generatedNamespace,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var orderedPolicies = PipelineOrdering.GetPoliciesInStableOrder(policies);

        for (var i = 0; i < orderedPolicies.Length; i++)
        {
            var policy = orderedPolicies[i];
            var pipelineName = GetPolicyPipelineTypeName(generatedNamespace, policy);

            PipelinePolicyCommandMap.AddFirstPolicyWins(map, policy.Commands, pipelineName);
        }

        return map;
    }

    private static string GetPolicyPipelineTypeName(string generatedNamespace, PolicySpec policy)
    {
        return "global::" +
            generatedNamespace +
            ".TinyDispatcherPolicyPipeline_" +
            PipelineNameFactory.SanitizePolicyName(policy.PolicyTypeFqn);
    }

    private static void AddPerCommandRegistrations(
        List<ServiceRegistration> registrations,
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        HashSet<string> perCommandSet)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(perCommandSet);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];

            registrations.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{coreNamespace}.ICommandPipeline<{command}, {contextTypeFqn}>",
                ImplementationTypeExpression: $"global::{generatedNamespace}.TinyDispatcherPipeline_{PipelineNameFactory.SanitizeCommandName(command)}"));
        }
    }

    private static void AddPolicyRegistrations(
        List<ServiceRegistration> registrations,
        string coreNamespace,
        string contextTypeFqn,
        HashSet<string> perCommandSet,
        HashSet<string> policyCommandSet,
        Dictionary<string, string> commandToPolicyPipeline)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(policyCommandSet);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];
            var hasPerCommandPipeline = perCommandSet.Contains(command);

            if (hasPerCommandPipeline)
            {
                continue;
            }

            registrations.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{coreNamespace}.ICommandPipeline<{command}, {contextTypeFqn}>",
                ImplementationTypeExpression: $"{commandToPolicyPipeline[command]}<{command}>"));
        }
    }

    private static void AddGlobalRegistrations(
        List<ServiceRegistration> registrations,
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        bool hasGlobal,
        DiscoveryResult discovery,
        HashSet<string> perCommandSet,
        HashSet<string> policyCommandSet)
    {
        if (!hasGlobal)
        {
            return;
        }

        var hasNoCommands = discovery.Commands.Length == 0;

        if (hasNoCommands)
        {
            return;
        }

        for (var i = 0; i < discovery.Commands.Length; i++)
        {
            AddGlobalRegistration(
                registrations,
                generatedNamespace,
                coreNamespace,
                contextTypeFqn,
                discovery.Commands[i],
                perCommandSet,
                policyCommandSet);
        }
    }

    private static void AddGlobalRegistration(
        List<ServiceRegistration> registrations,
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        HandlerContract commandHandler,
        HashSet<string> perCommandSet,
        HashSet<string> policyCommandSet)
    {
        var command = PipelineTypeNames.NormalizeFqn(commandHandler.MessageTypeFqn);
        var commandIsMissing = string.IsNullOrWhiteSpace(command);

        if (commandIsMissing)
        {
            return;
        }

        var hasPerCommandPipeline = perCommandSet.Contains(command);
        var hasPolicyPipeline = policyCommandSet.Contains(command);

        if (hasPerCommandPipeline || hasPolicyPipeline)
        {
            return;
        }

        registrations.Add(new ServiceRegistration(
            ServiceTypeExpression: $"{coreNamespace}.ICommandPipeline<{command}, {contextTypeFqn}>",
            ImplementationTypeExpression: $"global::{generatedNamespace}.TinyDispatcherGlobalPipeline<{command}>"));
    }

}
