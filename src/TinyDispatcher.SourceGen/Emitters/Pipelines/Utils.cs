#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class MiddlewareSets
{
    public static MiddlewareRef[] NormalizeDistinct(ImmutableArray<MiddlewareRef> items)
    {
        if (items.IsDefaultOrEmpty)
            return Array.Empty<MiddlewareRef>();

        var list = new List<MiddlewareRef>(items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            var x = items[i];

            // Struct: no null checks.
            var fqn = x.OpenTypeFqn;
            if (string.IsNullOrWhiteSpace(fqn))
                continue;

            // Preserve the symbol; normalize only the FQN.
            var normalizedFqn = TypeNames.NormalizeFqn(fqn);

            list.Add(new MiddlewareRef(
                OpenTypeSymbol: x.OpenTypeSymbol,
                OpenTypeFqn: normalizedFqn,
                Arity: x.Arity));
        }

        return DistinctByOpenTypeAndArity(list).ToArray();
    }

    public static IEnumerable<MiddlewareRef> DistinctByOpenTypeAndArity(IEnumerable<MiddlewareRef> items)
    {
        // Struct: no null checks.
        return items
            .Where(x => !string.IsNullOrWhiteSpace(x.OpenTypeFqn))
            .GroupBy(m => m.OpenTypeFqn + "|" + m.Arity.ToString(), StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(m => m.OpenTypeFqn, StringComparer.Ordinal);
    }
}

internal static class TypeNames
{
    public static string NormalizeFqn(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var trimmed = typeName.Trim();

        if (!trimmed.StartsWith("global::", StringComparison.Ordinal))
            trimmed = "global::" + trimmed;

        if (trimmed.StartsWith("global::global::", StringComparison.Ordinal))
            trimmed = "global::" + trimmed.Substring("global::global::".Length);

        return trimmed;
    }

    public static string CloseMiddleware(MiddlewareRef mw, string cmd, string ctx)
    {
        return mw.Arity == 2
            ? mw.OpenTypeFqn + "<" + cmd + ", " + ctx + ">"
            : mw.OpenTypeFqn + "<" + cmd + ">";
    }

    public static string OpenGenericTypeof(MiddlewareRef mw)
    {
        return mw.Arity == 2
            ? mw.OpenTypeFqn + "<,>"
            : mw.OpenTypeFqn + "<>";
    }
}

internal static class NameFactory
{
    public static string FieldName(MiddlewareRef mw) => "_" + CtorParamName(mw);

    public static string CtorParamName(MiddlewareRef mw)
    {
        var open = mw.OpenTypeFqn ?? string.Empty;

        var lastDot = open.LastIndexOf('.');
        var shortName = lastDot >= 0 ? open.Substring(lastDot + 1) : open;

        var tick = shortName.IndexOf('`');
        if (tick >= 0) shortName = shortName.Substring(0, tick);

        if (shortName.EndsWith("Middleware", StringComparison.Ordinal))
            shortName = shortName.Substring(0, shortName.Length - "Middleware".Length);

        if (string.IsNullOrWhiteSpace(shortName))
            shortName = "Middleware";

        return shortName.Length == 1
            ? char.ToLowerInvariant(shortName[0]).ToString()
            : char.ToLowerInvariant(shortName[0]) + shortName.Substring(1);
    }

    public static string SanitizeCommandName(string cmdFqn)
    {
        var s = cmdFqn.StartsWith("global::", StringComparison.Ordinal)
            ? cmdFqn.Substring("global::".Length)
            : cmdFqn;

        var lastDot = s.LastIndexOf('.');
        var name = lastDot >= 0 ? s.Substring(lastDot + 1) : s;

        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }

    public static string SanitizePolicyName(string policyTypeFqn)
    {
        var s = policyTypeFqn.StartsWith("global::", StringComparison.Ordinal)
            ? policyTypeFqn.Substring("global::".Length)
            : policyTypeFqn;

        return new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
    }
}
