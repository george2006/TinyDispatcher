using System;

namespace TinyDispatcher;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class TinyDispatcherAssemblyContextContributionAttribute : Attribute
{
    public TinyDispatcherAssemblyContextContributionAttribute(Type contextType)
    {
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    public Type ContextType { get; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TinyDispatcherHandlerContributionAttribute : Attribute
{
    public TinyDispatcherHandlerContributionAttribute(Type commandType, Type handlerType, Type contextType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    public Type CommandType { get; }

    public Type HandlerType { get; }

    public Type ContextType { get; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TinyDispatcherPipelineContributionAttribute : Attribute
{
    public TinyDispatcherPipelineContributionAttribute(Type[] middlewareTypes)
    {
        MiddlewareTypes = middlewareTypes ?? Array.Empty<Type>();
    }

    public Type? CommandType { get; set; }

    public Type[] MiddlewareTypes { get; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TinyDispatcherPolicyContributionAttribute : Attribute
{
    public TinyDispatcherPolicyContributionAttribute(
        Type policyType,
        Type[] middlewareTypes,
        Type[] commandTypes)
    {
        PolicyType = policyType ?? throw new ArgumentNullException(nameof(policyType));
        MiddlewareTypes = middlewareTypes ?? Array.Empty<Type>();
        CommandTypes = commandTypes ?? Array.Empty<Type>();
    }

    public Type PolicyType { get; }

    public Type[] MiddlewareTypes { get; }

    public Type[] CommandTypes { get; }
}
