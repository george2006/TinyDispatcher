using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelinePlanner
{
    private static readonly MiddlewareRef[] NoMiddlewares = Array.Empty<MiddlewareRef>();

    public static PipelinePlan Build(
        PipelineContributions contributions,
        DiscoveryResult discovery,
        GeneratorOptions options)
    {
        var coreNamespace = "global::TinyDispatcher";
        var generatedNamespace = options.GeneratedNamespace;
        var contextType = PipelineTypeNames.NormalizeFqn(options.CommandContextType!);
        var pipelineClassSuffix = BuildPipelineClassSuffix(contextType);

        var global = contributions.Globals;
        var hasGlobalMiddlewares = global.Length > 0;

        var perCommandMiddlewares = contributions.PerCommand;
        var policies = contributions.Policies;
        var globalPipeline = BuildGlobalPipeline(global, pipelineClassSuffix);

        var policyPipelines = BuildPolicyPipelines(global, policies, pipelineClassSuffix);
        var perCommandPipelines = BuildPerCommandPipelines(
            global,
            perCommandMiddlewares,
            contributions.PolicyByCommand,
            pipelineClassSuffix);

        var middlewareRegistrations = BuildOpenGenericMiddlewareRegistrations(
            global,
            perCommandMiddlewares,
            policies);

        var serviceRegistrations = PipelineRegistrationPlanner.Build(
            generatedNamespace,
            coreNamespace,
            contextType,
            discovery,
            globalPipeline,
            policyPipelines,
            perCommandPipelines);
        var resolvedPipelines = ResolvePipelines(
            serviceRegistrations,
            discovery);

        var shouldEmit = ShouldEmitPlan(
            globalPipeline,
            policyPipelines,
            perCommandPipelines,
            middlewareRegistrations,
            serviceRegistrations);

        return new PipelinePlan(
            GeneratedNamespace: generatedNamespace,
            ContextFqn: contextType,
            CoreFqn: coreNamespace,
            ShouldEmit: shouldEmit,
            GlobalPipeline: globalPipeline,
            PolicyPipelines: policyPipelines,
            PerCommandPipelines: perCommandPipelines,
            OpenGenericMiddlewareRegistrations: middlewareRegistrations,
            ServiceRegistrations: serviceRegistrations,
            ResolvedPipelines: resolvedPipelines);
    }

    private static ImmutableArray<ResolvedPipeline> ResolvePipelines(
        ImmutableArray<PipelineRegistration> registrations,
        DiscoveryResult discovery)
    {
        var handlersByCommand = new Dictionary<string, HandlerContract>(StringComparer.Ordinal);

        for (var i = 0; i < discovery.Commands.Length; i++)
        {
            var handler = discovery.Commands[i];
            var command = PipelineTypeNames.NormalizeFqn(handler.MessageTypeFqn);
            handlersByCommand[command] = handler;
        }

        var pipelines = ImmutableArray.CreateBuilder<ResolvedPipeline>();

        for (var i = 0; i < registrations.Length; i++)
        {
            var registration = registrations[i];
            if (!handlersByCommand.TryGetValue(registration.CommandType, out var handler))
            {
                continue;
            }

            pipelines.Add(new ResolvedPipeline(
                handler,
                registration.Pipeline));
        }

        return pipelines.ToImmutable();
    }

    private static PipelineDefinition? BuildGlobalPipeline(
        MiddlewareRef[] global,
        string pipelineClassSuffix)
    {
        var hasGlobalMiddlewares = global.Length > 0;

        if (!hasGlobalMiddlewares)
        {
            return null;
        }

        return new PipelineDefinition(
            ClassName: "TinyDispatcherGlobalPipeline" + pipelineClassSuffix,
            IsOpenGeneric: true,
            CommandType: "TCommand",
            Steps: BuildSteps(global, policy: null, NoMiddlewares));
    }

    private static ImmutableArray<PolicyPipelineDefinition> BuildPolicyPipelines(
        MiddlewareRef[] global,
        PipelinePolicyContribution[] policies,
        string pipelineClassSuffix)
    {
        if (policies.Length == 0)
        {
            return ImmutableArray<PolicyPipelineDefinition>.Empty;
        }

        var list = new List<PolicyPipelineDefinition>(policies.Length);

        for (var i = 0; i < policies.Length; i++)
        {
            var policy = policies[i];

            list.Add(new PolicyPipelineDefinition(
                Policy: policy,
                Pipeline: new PipelineDefinition(
                    ClassName: "TinyDispatcherPolicyPipeline_" +
                        PipelineNameFactory.SanitizePolicyName(policy.PolicyTypeFqn) +
                        pipelineClassSuffix,
                    IsOpenGeneric: true,
                    CommandType: "TCommand",
                    Steps: BuildSteps(global, policy, NoMiddlewares))));
        }

        return list.ToImmutableArray();
    }

    private static ImmutableArray<PipelineDefinition> BuildPerCommandPipelines(
        MiddlewareRef[] global,
        IReadOnlyDictionary<string, MiddlewareRef[]> perCommandMiddlewares,
        IReadOnlyDictionary<string, PipelinePolicyContribution> policyByCommand,
        string pipelineClassSuffix)
    {
        if (perCommandMiddlewares.Count == 0)
        {
            return ImmutableArray<PipelineDefinition>.Empty;
        }

        var list = new List<PipelineDefinition>(perCommandMiddlewares.Count);

        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(perCommandMiddlewares.Keys);
        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var commandFqn = orderedCommands[i];
            var commandMiddlewares = perCommandMiddlewares[commandFqn];
            PipelinePolicyContribution? policy;

            if (!policyByCommand.TryGetValue(commandFqn, out policy))
            {
                policy = null;
            }

            list.Add(new PipelineDefinition(
                ClassName: "TinyDispatcherPipeline_" +
                    PipelineNameFactory.SanitizeCommandName(commandFqn) +
                    pipelineClassSuffix,
                IsOpenGeneric: false,
                CommandType: commandFqn,
                Steps: BuildSteps(global, policy, commandMiddlewares)));
        }

        return list.ToImmutableArray();
    }

    private static ImmutableArray<MiddlewareStep> BuildSteps(
        MiddlewareRef[] global,
        PipelinePolicyContribution? policy,
        MiddlewareRef[] perCommand)
    {
        var policyMiddlewares = policy?.Middlewares ?? NoMiddlewares;
        var steps = new List<MiddlewareStep>(global.Length + policyMiddlewares.Length + perCommand.Length);

        AddSteps(steps, global, PipelineStepSource.Global);
        AddSteps(
            steps,
            policyMiddlewares,
            PipelineStepSource.Policy,
            policy?.PolicyTypeFqn);
        AddSteps(steps, perCommand, PipelineStepSource.Operation);

        return steps.ToImmutableArray();
    }

    private static void AddSteps(
        List<MiddlewareStep> steps,
        MiddlewareRef[] middlewares,
        PipelineStepSource source,
        string? policyTypeFqn = null)
    {
        for (var i = 0; i < middlewares.Length; i++)
        {
            steps.Add(new MiddlewareStep(
                middlewares[i],
                source,
                policyTypeFqn));
        }
    }

    private static ImmutableArray<OpenGenericRegistration> BuildOpenGenericMiddlewareRegistrations(
        MiddlewareRef[] global,
        IReadOnlyDictionary<string, MiddlewareRef[]> perCommand,
        PipelinePolicyContribution[] policies)
    {
        var all = new List<MiddlewareRef>(256);

        all.AddRange(global);

        foreach (var pair in perCommand)
        {
            all.AddRange(pair.Value);
        }

        for (var i = 0; i < policies.Length; i++)
        {
            all.AddRange(policies[i].Middlewares);
        }

        var distinct = PipelineMiddlewareSets.NormalizeDistinct(all.ToImmutableArray());

        var regs = new List<OpenGenericRegistration>(distinct.Length);
        for (var i = 0; i < distinct.Length; i++)
        {
            regs.Add(new OpenGenericRegistration(PipelineTypeNames.OpenGenericTypeof(distinct[i])));
        }

        return regs.ToImmutableArray();
    }

    private static bool ShouldEmitPlan(
        PipelineDefinition? globalPipeline,
        ImmutableArray<PolicyPipelineDefinition> policyPipelines,
        ImmutableArray<PipelineDefinition> perCommandPipelines,
        ImmutableArray<OpenGenericRegistration> middlewareRegistrations,
        ImmutableArray<PipelineRegistration> serviceRegistrations)
    {
        var hasGlobalPipeline = globalPipeline is not null;
        var hasPolicyPipelines = policyPipelines.Length > 0;
        var hasPerCommandPipelines = perCommandPipelines.Length > 0;
        var hasMiddlewareRegistrations = middlewareRegistrations.Length > 0;
        var hasServiceRegistrations = serviceRegistrations.Length > 0;

        return hasGlobalPipeline ||
            hasPolicyPipelines ||
            hasPerCommandPipelines ||
            hasMiddlewareRegistrations ||
            hasServiceRegistrations;
    }

    private static string BuildPipelineClassSuffix(string contextTypeFqn)
    {
        if (string.IsNullOrWhiteSpace(contextTypeFqn))
        {
            return string.Empty;
        }

        return "_" + PipelineNameFactory.SanitizeTypeName(contextTypeFqn);
    }
}

