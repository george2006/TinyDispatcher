using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineRegistrationPlanner
{
    public static ImmutableArray<ServiceRegistration> Build(
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        bool hasGlobal,
        DiscoveryResult discovery,
        IReadOnlyDictionary<string, MiddlewareRef[]> perCommand,
        PipelinePolicyContribution[] policies)
    {
        var commandToPolicyPipeline = BuildCommandToPolicyPipelineNames(generatedNamespace, policies);
        var state = new PipelineRegistrationState(
            GeneratedNamespace: generatedNamespace,
            CoreNamespace: coreNamespace,
            ContextTypeFqn: contextTypeFqn,
            HasGlobal: hasGlobal,
            Discovery: discovery,
            PerCommandSet: new HashSet<string>(perCommand.Keys, StringComparer.Ordinal),
            PolicyCommandSet: new HashSet<string>(commandToPolicyPipeline.Keys, StringComparer.Ordinal),
            CommandToPolicyPipeline: commandToPolicyPipeline);

        var registrations = new List<ServiceRegistration>(256);

        AddPerCommandRegistrations(registrations, state);
        AddPolicyRegistrations(registrations, state);
        AddGlobalRegistrations(registrations, state);

        return registrations.ToImmutableArray();
    }

    private static Dictionary<string, string> BuildCommandToPolicyPipelineNames(
        string generatedNamespace,
        PipelinePolicyContribution[] policies)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < policies.Length; i++)
        {
            var policy = policies[i];
            var pipelineName = GetPolicyPipelineTypeName(generatedNamespace, policy);

            PipelinePolicyCommandMap.AddFirstPolicyWins(map, policy.Commands, pipelineName);
        }

        return map;
    }

    private static string GetPolicyPipelineTypeName(string generatedNamespace, PipelinePolicyContribution policy)
    {
        return "global::" +
            generatedNamespace +
            ".TinyDispatcherPolicyPipeline_" +
            PipelineNameFactory.SanitizePolicyName(policy.PolicyTypeFqn);
    }

    private static void AddPerCommandRegistrations(
        List<ServiceRegistration> registrations,
        PipelineRegistrationState state)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(state.PerCommandSet);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];

            registrations.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{state.CoreNamespace}.ICommandPipeline<{command}, {state.ContextTypeFqn}>",
                ImplementationTypeExpression: $"global::{state.GeneratedNamespace}.TinyDispatcherPipeline_{PipelineNameFactory.SanitizeCommandName(command)}"));
        }
    }

    private static void AddPolicyRegistrations(
        List<ServiceRegistration> registrations,
        PipelineRegistrationState state)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(state.PolicyCommandSet);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];
            var hasPerCommandPipeline = state.PerCommandSet.Contains(command);

            if (hasPerCommandPipeline)
            {
                continue;
            }

            registrations.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{state.CoreNamespace}.ICommandPipeline<{command}, {state.ContextTypeFqn}>",
                ImplementationTypeExpression: $"{state.CommandToPolicyPipeline[command]}<{command}>"));
        }
    }

    private static void AddGlobalRegistrations(
        List<ServiceRegistration> registrations,
        PipelineRegistrationState state)
    {
        if (!state.HasGlobal)
        {
            return;
        }

        var hasNoCommands = state.Discovery.Commands.Length == 0;

        if (hasNoCommands)
        {
            return;
        }

        for (var i = 0; i < state.Discovery.Commands.Length; i++)
        {
            AddGlobalRegistration(
                registrations,
                state,
                state.Discovery.Commands[i]);
        }
    }

    private static void AddGlobalRegistration(
        List<ServiceRegistration> registrations,
        PipelineRegistrationState state,
        HandlerContract commandHandler)
    {
        var command = PipelineTypeNames.NormalizeFqn(commandHandler.MessageTypeFqn);
        var commandIsMissing = string.IsNullOrWhiteSpace(command);

        if (commandIsMissing)
        {
            return;
        }

        var hasPerCommandPipeline = state.PerCommandSet.Contains(command);
        var hasPolicyPipeline = state.PolicyCommandSet.Contains(command);

        if (hasPerCommandPipeline || hasPolicyPipeline)
        {
            return;
        }

        registrations.Add(new ServiceRegistration(
            ServiceTypeExpression: $"{state.CoreNamespace}.ICommandPipeline<{command}, {state.ContextTypeFqn}>",
            ImplementationTypeExpression: $"global::{state.GeneratedNamespace}.TinyDispatcherGlobalPipeline<{command}>"));
    }

    private sealed record PipelineRegistrationState(
        string GeneratedNamespace,
        string CoreNamespace,
        string ContextTypeFqn,
        bool HasGlobal,
        DiscoveryResult Discovery,
        HashSet<string> PerCommandSet,
        HashSet<string> PolicyCommandSet,
        Dictionary<string, string> CommandToPolicyPipeline);
}

