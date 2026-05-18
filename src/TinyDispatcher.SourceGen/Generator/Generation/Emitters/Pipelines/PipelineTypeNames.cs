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

    public static string CloseMiddleware(
        MiddlewareRef middleware,
        string commandType,
        string contextType)
    {
        if (UsesOpenContextParameter(middleware))
        {
            return middleware.OpenTypeFqn + "<" + commandType + ", " + contextType + ">";
        }

        return middleware.OpenTypeFqn + "<" + commandType + ">";
    }

    public static string OpenGenericTypeof(MiddlewareRef middleware)
    {
        if (UsesOpenContextParameter(middleware))
        {
            return middleware.OpenTypeFqn + "<,>";
        }

        return middleware.OpenTypeFqn + "<>";
    }

    private static bool UsesOpenContextParameter(MiddlewareRef middleware)
    {
        return middleware.Arity == 2;
    }
}

