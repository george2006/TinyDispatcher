#nullable enable

using System;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineNameFactory
{
    public static string FieldName(MiddlewareRef mw)
    {
        return "_" + CtorParamName(mw);
    }

    public static string CtorParamName(MiddlewareRef mw)
    {
        var open = mw.OpenTypeFqn ?? string.Empty;

        var shortName = GetNameAfterLastDot(open);

        var tick = shortName.IndexOf('`');
        if (tick >= 0)
        {
            shortName = shortName.Substring(0, tick);
        }

        if (shortName.EndsWith("Middleware", StringComparison.Ordinal))
        {
            shortName = shortName.Substring(0, shortName.Length - "Middleware".Length);
        }

        if (string.IsNullOrWhiteSpace(shortName))
        {
            shortName = "Middleware";
        }

        return ToCamelCase(shortName);
    }

    public static string SanitizeCommandName(string cmdFqn)
    {
        var typeName = RemoveGlobalPrefix(cmdFqn);
        var name = GetNameAfterLastDot(typeName);

        return SanitizeName(name);
    }

    public static string SanitizePolicyName(string policyTypeFqn)
    {
        return SanitizeTypeName(policyTypeFqn);
    }

    public static string SanitizeTypeName(string typeFqn)
    {
        var typeName = RemoveGlobalPrefix(typeFqn);

        return SanitizeName(typeName);
    }

    private static string RemoveGlobalPrefix(string value)
    {
        if (value.StartsWith("global::", StringComparison.Ordinal))
        {
            return value.Substring("global::".Length);
        }

        return value;
    }

    private static string GetNameAfterLastDot(string value)
    {
        var lastDot = value.LastIndexOf('.');

        if (lastDot >= 0)
        {
            return value.Substring(lastDot + 1);
        }

        return value;
    }

    private static string ToCamelCase(string value)
    {
        if (value.Length == 1)
        {
            return char.ToLowerInvariant(value[0]).ToString();
        }

        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var chars = new char[value.Length];

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                chars[i] = c;
            }
            else
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}

