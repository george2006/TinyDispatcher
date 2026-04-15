#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelineMiddlewareSets
{
    public static MiddlewareRef[] NormalizeDistinct(ImmutableArray<MiddlewareRef> items)
    {
        if (items.IsDefaultOrEmpty)
            return Array.Empty<MiddlewareRef>();

        var list = new List<MiddlewareRef>(items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            var x = items[i];

            var fqn = x.OpenTypeFqn;
            if (string.IsNullOrWhiteSpace(fqn))
                continue;

            var normalizedFqn = PipelineTypeNames.NormalizeFqn(fqn);

            list.Add(new MiddlewareRef(
                OpenTypeSymbol: x.OpenTypeSymbol,
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
                continue;

            var key = fqn + "|" + item.Arity.ToString(CultureInfo.InvariantCulture);
            if (seen.Add(key))
                distinct.Add(item);
        }

        StableSortByOpenTypeFqn(distinct);

        return distinct.ToArray();
    }

    private static void StableSortByOpenTypeFqn(List<MiddlewareRef> items)
    {
        for (var i = 1; i < items.Count; i++)
        {
            var current = items[i];
            var j = i - 1;

            while (j >= 0 && string.Compare(items[j].OpenTypeFqn, current.OpenTypeFqn, StringComparison.Ordinal) > 0)
            {
                items[j + 1] = items[j];
                j--;
            }

            items[j + 1] = current;
        }
    }
}
