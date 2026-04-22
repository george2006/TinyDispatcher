using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineOrdering
{
    public static string[] GetStringsInStableOrder(IEnumerable<string> values)
    {
        var ordered = new List<string>();
        foreach (var value in values)
        {
            ordered.Add(value);
        }

        ordered.Sort(StringComparer.Ordinal);

        return CopyStringsToArray(ordered);
    }

    public static PolicySpec[] GetPoliciesInStableOrder(ImmutableDictionary<string, PolicySpec> policies)
    {
        if (policies.Count == 0)
        {
            return Array.Empty<PolicySpec>();
        }

        var ordered = new List<PolicySpec>(policies.Count);
        foreach (var policy in policies.Values)
        {
            ordered.Add(policy);
        }

        ordered.Sort(ComparePolicies);

        return CopyPoliciesToArray(ordered);
    }

    private static int ComparePolicies(PolicySpec left, PolicySpec right)
    {
        var leftPolicyName = PipelineTypeNames.NormalizeFqn(left.PolicyTypeFqn);
        var rightPolicyName = PipelineTypeNames.NormalizeFqn(right.PolicyTypeFqn);

        return string.Compare(leftPolicyName, rightPolicyName, StringComparison.Ordinal);
    }

    private static string[] CopyStringsToArray(List<string> values)
    {
        var result = new string[values.Count];

        for (var i = 0; i < values.Count; i++)
        {
            result[i] = values[i];
        }

        return result;
    }

    private static PolicySpec[] CopyPoliciesToArray(List<PolicySpec> policies)
    {
        var result = new PolicySpec[policies.Count];

        for (var i = 0; i < policies.Count; i++)
        {
            result[i] = policies[i];
        }

        return result;
    }
}

