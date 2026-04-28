#nullable enable

using System;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal static class PipelineTypeNames
{
    public static string NormalizeFqn(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return string.Empty;
        }

        var trimmed = typeName.Trim();

        if (!trimmed.StartsWith("global::", StringComparison.Ordinal))
        {
            trimmed = "global::" + trimmed;
        }

        if (trimmed.StartsWith("global::global::", StringComparison.Ordinal))
        {
            trimmed = "global::" + trimmed.Substring("global::global::".Length);
        }

        return trimmed;
    }

    public static string CloseMiddleware(MiddlewareRef mw, string cmd, string ctx)
    {
        if (mw.Arity == 2)
        {
            return mw.OpenTypeFqn + "<" + cmd + ", " + ctx + ">";
        }

        return mw.OpenTypeFqn + "<" + cmd + ">";
    }

    public static string OpenGenericTypeof(MiddlewareRef mw)
    {
        if (mw.Arity == 2)
        {
            return mw.OpenTypeFqn + "<,>";
        }

        return mw.OpenTypeFqn + "<>";
    }
}

