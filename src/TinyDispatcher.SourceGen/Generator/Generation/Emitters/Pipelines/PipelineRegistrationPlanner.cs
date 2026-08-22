using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineRegistrationPlanner
{
    public static ImmutableArray<PipelineRegistration> Build(
        string generatedNamespace,
        string coreNamespace,
        string contextTypeFqn,
        DiscoveryResult discovery,
        PipelineDefinition? globalPipeline,
        ImmutableArray<PolicyPipelineDefinition> policyPipelines,
        ImmutableArray<PipelineDefinition> perCommandPipelines)
    {
        var perCommandPipelineByCommand = BuildPerCommandPipelineMap(perCommandPipelines);
        var policyPipelineByCommand = BuildPolicyPipelineMap(policyPipelines);
        var state = new PipelineRegistrationState(
            GeneratedNamespace: generatedNamespace,
            CoreNamespace: coreNamespace,
            ContextTypeFqn: contextTypeFqn,
            Discovery: discovery,
            GlobalPipeline: globalPipeline,
            PerCommandPipelineByCommand: perCommandPipelineByCommand,
            PolicyPipelineByCommand: policyPipelineByCommand);

        var registrations = new List<PipelineRegistration>(256);

        AddPerCommandRegistrations(registrations, state);
        AddPolicyRegistrations(registrations, state);
        AddGlobalRegistrations(registrations, state);

        return registrations.ToImmutableArray();
    }

    private static Dictionary<string, PipelineDefinition> BuildPerCommandPipelineMap(
        ImmutableArray<PipelineDefinition> pipelines)
    {
        var map = new Dictionary<string, PipelineDefinition>(StringComparer.Ordinal);

        for (var i = 0; i < pipelines.Length; i++)
        {
            var pipeline = pipelines[i];
            map[pipeline.CommandType] = pipeline;
        }

        return map;
    }

    private static Dictionary<string, PipelineDefinition> BuildPolicyPipelineMap(
        ImmutableArray<PolicyPipelineDefinition> pipelines)
    {
        var map = new Dictionary<string, PipelineDefinition>(StringComparer.Ordinal);

        for (var i = 0; i < pipelines.Length; i++)
        {
            PipelinePolicyCommandMap.AddFirstPolicyWins(
                map,
                pipelines[i].Policy.Commands,
                pipelines[i].Pipeline);
        }

        return map;
    }

    private static void AddPerCommandRegistrations(
        List<PipelineRegistration> registrations,
        PipelineRegistrationState state)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(
            state.PerCommandPipelineByCommand.Keys);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];
            registrations.Add(CreateRegistration(
                state,
                command,
                state.PerCommandPipelineByCommand[command]));
        }
    }

    private static void AddPolicyRegistrations(
        List<PipelineRegistration> registrations,
        PipelineRegistrationState state)
    {
        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(
            state.PolicyPipelineByCommand.Keys);

        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var command = orderedCommands[i];
            var hasPerCommandPipeline = state.PerCommandPipelineByCommand.ContainsKey(command);

            if (hasPerCommandPipeline)
            {
                continue;
            }

            registrations.Add(CreateRegistration(
                state,
                command,
                state.PolicyPipelineByCommand[command]));
        }
    }

    private static void AddGlobalRegistrations(
        List<PipelineRegistration> registrations,
        PipelineRegistrationState state)
    {
        if (state.GlobalPipeline is null)
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
        List<PipelineRegistration> registrations,
        PipelineRegistrationState state,
        HandlerContract commandHandler)
    {
        var command = PipelineTypeNames.NormalizeFqn(commandHandler.MessageTypeFqn);
        var commandIsMissing = string.IsNullOrWhiteSpace(command);

        if (commandIsMissing)
        {
            return;
        }

        var hasPerCommandPipeline = state.PerCommandPipelineByCommand.ContainsKey(command);
        var hasPolicyPipeline = state.PolicyPipelineByCommand.ContainsKey(command);

        if (hasPerCommandPipeline || hasPolicyPipeline)
        {
            return;
        }

        registrations.Add(CreateRegistration(
            state,
            command,
            state.GlobalPipeline!));
    }

    private static PipelineRegistration CreateRegistration(
        PipelineRegistrationState state,
        string command,
        PipelineDefinition pipeline)
    {
        var implementationType = $"global::{state.GeneratedNamespace}.{pipeline.ClassName}";
        if (pipeline.IsOpenGeneric)
        {
            implementationType += $"<{command}>";
        }

        return new PipelineRegistration(
            CommandType: command,
            Pipeline: pipeline,
            ServiceTypeExpression: $"{state.CoreNamespace}.ICommandPipeline<{command}, {state.ContextTypeFqn}>",
            ImplementationTypeExpression: implementationType);
    }

    private sealed record PipelineRegistrationState(
        string GeneratedNamespace,
        string CoreNamespace,
        string ContextTypeFqn,
        DiscoveryResult Discovery,
        PipelineDefinition? GlobalPipeline,
        Dictionary<string, PipelineDefinition> PerCommandPipelineByCommand,
        Dictionary<string, PipelineDefinition> PolicyPipelineByCommand);
}

