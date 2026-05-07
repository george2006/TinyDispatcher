#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineMiddlewareSets
{
    public static MiddlewareRef[] NormalizeDistinct(ImmutableArray<MiddlewareRef> items)
    {
        if (items.IsDefaultOrEmpty)
        {
            return Array.Empty<MiddlewareRef>();
        }

        var list = new List<MiddlewareRef>(items.Length);

        for (var i = 0; i < items.Length; i++)
        {
            var x = items[i];

            var fqn = x.OpenTypeFqn;
            if (string.IsNullOrWhiteSpace(fqn))
            {
                continue;
            }

            var normalizedFqn = PipelineTypeNames.NormalizeFqn(fqn);

            list.Add(new MiddlewareRef(
                OpenTypeFqn: normalizedFqn,
                Arity: x.Arity));
        }

        return DistinctByOpenTypeAndArity(list);
    }

    public static MiddlewareRef[] DistinctByOpenTypeAndArity(IEnumerable<MiddlewareRef> items)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var distinct = new List<MiddlewareRef>();

        foreach (var item in items)
        {
            var fqn = item.OpenTypeFqn;
            if (string.IsNullOrWhiteSpace(fqn))
            {
                continue;
            }

            var key = fqn + "|" + item.Arity.ToString(CultureInfo.InvariantCulture);
            if (seen.Add(key))
            {
                distinct.Add(item);
            }
        }

        distinct.Sort(CompareByOpenTypeFqn);

        return distinct.ToArray();
    }

    private static int CompareByOpenTypeFqn(MiddlewareRef left, MiddlewareRef right)
    {
        return string.Compare(left.OpenTypeFqn, right.OpenTypeFqn, StringComparison.Ordinal);
    }
}

