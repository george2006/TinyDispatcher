#nullable enable

using System;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelineNameFactory
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

        return SanitizeName(name);
    }

    public static string SanitizePolicyName(string policyTypeFqn)
    {
        var s = policyTypeFqn.StartsWith("global::", StringComparison.Ordinal)
            ? policyTypeFqn.Substring("global::".Length)
            : policyTypeFqn;

        return SanitizeName(s);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var chars = new char[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            chars[i] = char.IsLetterOrDigit(c) ? c : '_';
        }

        return new string(chars);
    }
}
