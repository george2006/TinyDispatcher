#nullable enable

using System;

namespace TinyDispatcher;

/// <summary>
/// Describes a generated command pipeline and the commands that use it.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TinyDispatcherPipelineMapAttribute : Attribute
{
    public TinyDispatcherPipelineMapAttribute(
        Type pipelineType,
        Type contextType,
        Type[] commandTypes,
        Type[] middlewareTypes,
        int globalMiddlewareCount,
        Type? policyType,
        int policyMiddlewareCount)
    {
        PipelineType = pipelineType ?? throw new ArgumentNullException(nameof(pipelineType));
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
        CommandTypes = CopyTypes(commandTypes, nameof(commandTypes));
        MiddlewareTypes = CopyTypes(middlewareTypes, nameof(middlewareTypes));

        if (CommandTypes.Length == 0)
        {
            throw new ArgumentException("Pipeline map requires at least one command type.", nameof(commandTypes));
        }

        ValidateMiddlewareCounts(
            MiddlewareTypes.Length,
            globalMiddlewareCount,
            policyType,
            policyMiddlewareCount);

        GlobalMiddlewareCount = globalMiddlewareCount;
        PolicyType = policyType;
        PolicyMiddlewareCount = policyMiddlewareCount;
    }

    public Type PipelineType { get; }

    public Type ContextType { get; }

    public Type[] CommandTypes { get; }

    public Type[] MiddlewareTypes { get; }

    public int GlobalMiddlewareCount { get; }

    public Type? PolicyType { get; }

    public int PolicyMiddlewareCount { get; }

    private static Type[] CopyTypes(Type[] values, string parameterName)
    {
        if (values is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var copy = new Type[values.Length];

        for (var i = 0; i < values.Length; i++)
        {
            copy[i] = values[i]
                ?? throw new ArgumentException("Type collection cannot contain null values.", parameterName);
        }

        return copy;
    }

    private static void ValidateMiddlewareCounts(
        int middlewareCount,
        int globalMiddlewareCount,
        Type? policyType,
        int policyMiddlewareCount)
    {
        if (globalMiddlewareCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(globalMiddlewareCount));
        }

        if (policyMiddlewareCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policyMiddlewareCount));
        }

        if (globalMiddlewareCount + policyMiddlewareCount > middlewareCount)
        {
            throw new ArgumentException("Middleware source counts exceed the middleware collection.");
        }

        if (policyMiddlewareCount > 0 && policyType is null)
        {
            throw new ArgumentException(
                "Policy middleware requires a policy type.",
                nameof(policyType));
        }
    }
}
