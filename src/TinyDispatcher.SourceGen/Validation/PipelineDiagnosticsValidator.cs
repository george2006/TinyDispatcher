using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class PipelineDiagnosticsValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));
        if (diags is null) throw new ArgumentNullException(nameof(diags));

        var discoveredCommands = BuildDiscoveredCommandSet(context.DiscoveryResult);

        ValidatePerCommandMiddlewareTargets(context, diags, discoveredCommands);
        ValidatePolicyTargets(context, diags, discoveredCommands);
        ValidateMultiplePoliciesPerCommand(context, diags);
    }

    private static HashSet<string> BuildDiscoveredCommandSet(DiscoveryResult discovery)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < discovery.Commands.Length; i++)
        {
            var fqn = TypeNames.NormalizeFqn(discovery.Commands[i].MessageTypeFqn);
            if (!string.IsNullOrWhiteSpace(fqn))
                set.Add(fqn);
        }

        return set;
    }

    private static void ValidatePerCommandMiddlewareTargets(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        HashSet<string> discoveredCommands)
    {
        if (context.PerCommand.Count == 0) return;

        foreach (var kv in context.PerCommand)
        {
            var cmd = TypeNames.NormalizeFqn(kv.Key);
            if (string.IsNullOrWhiteSpace(cmd)) continue;

            if (!discoveredCommands.Contains(cmd))
            {
                diags.Add(context.Diagnostics.Create(
                    context.Diagnostics.MiddlewareConfiguredForUnknownCommand,
                    Location.None,
                    cmd));
            }
        }
    }

    private static void ValidatePolicyTargets(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        HashSet<string> discoveredCommands)
    {
        if (context.Policies.Count == 0) return;

        foreach (var p in context.Policies.Values)
        {
            var policyType = TypeNames.NormalizeFqn(p.PolicyTypeFqn);

            for (int i = 0; i < p.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (!discoveredCommands.Contains(cmd))
                {
                    diags.Add(context.Diagnostics.Create(
                        context.Diagnostics.PolicyTargetsUnknownCommand,
                        Location.None,
                        policyType,
                        cmd));
                }
            }
        }
    }

    private static void ValidateMultiplePoliciesPerCommand(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        if (context.Policies.Count == 0) return;

        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var p in context.Policies.Values)
        {
            var policyType = TypeNames.NormalizeFqn(p.PolicyTypeFqn);
            if (string.IsNullOrWhiteSpace(policyType)) continue;

            for (int i = 0; i < p.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (!map.TryGetValue(cmd, out var list))
                {
                    list = new List<string>(4);
                    map[cmd] = list;
                }

                list.Add(policyType);
            }
        }

        foreach (var kv in map)
        {
            if (kv.Value.Count <= 1) continue;

            var policies = string.Join(", ", kv.Value.Distinct(StringComparer.Ordinal));

            diags.Add(context.Diagnostics.Create(
                context.Diagnostics.MultiplePoliciesForSameCommand,
                Location.None,
                kv.Key,
                policies));
        }
    }
}
