using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher.Bootstrap;

public class AssemblyContribution
{
    public AssemblyContribution(
        Type? contextType = null,
        Action<IServiceCollection>? registerServices = null,
        IReadOnlyList<HandlerBinding>? handlers = null,
        IReadOnlyList<PipelineBinding>? pipelines = null,
        IReadOnlyList<PolicyBinding>? policies = null)
    {
        ContextType = contextType;
        RegisterServices = registerServices;
        Handlers = handlers ?? Array.Empty<HandlerBinding>();
        Pipelines = pipelines ?? Array.Empty<PipelineBinding>();
        Policies = policies ?? Array.Empty<PolicyBinding>();
    }

    public Type? ContextType { get; }

    public Action<IServiceCollection>? RegisterServices { get; }

    public IReadOnlyList<HandlerBinding> Handlers { get; }

    public IReadOnlyList<PipelineBinding> Pipelines { get; }

    public IReadOnlyList<PolicyBinding> Policies { get; }

    public virtual void Apply(IServiceCollection services)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        RegisterServices?.Invoke(services);
    }
}

public sealed class HandlerBinding
{
    public HandlerBinding(Type commandType, Type handlerType, Type contextType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        HandlerType = handlerType ?? throw new ArgumentNullException(nameof(handlerType));
        ContextType = contextType ?? throw new ArgumentNullException(nameof(contextType));
    }

    public Type CommandType { get; }

    public Type HandlerType { get; }

    public Type ContextType { get; }
}

public sealed class PipelineBinding
{
    public PipelineBinding(Type? commandType, IReadOnlyList<MiddlewareBinding>? middlewares = null)
    {
        CommandType = commandType;
        Middlewares = middlewares ?? Array.Empty<MiddlewareBinding>();
    }

    public Type? CommandType { get; }

    public IReadOnlyList<MiddlewareBinding> Middlewares { get; }
}

public sealed class MiddlewareBinding
{
    public MiddlewareBinding(Type middlewareType)
    {
        MiddlewareType = middlewareType ?? throw new ArgumentNullException(nameof(middlewareType));
    }

    public Type MiddlewareType { get; }
}

public sealed class PolicyBinding
{
    public PolicyBinding(
        Type policyType,
        IReadOnlyList<MiddlewareBinding>? middlewares = null,
        IReadOnlyList<PolicyCommandBinding>? commands = null)
    {
        PolicyType = policyType ?? throw new ArgumentNullException(nameof(policyType));
        Middlewares = middlewares ?? Array.Empty<MiddlewareBinding>();
        Commands = commands ?? Array.Empty<PolicyCommandBinding>();
    }

    public Type PolicyType { get; }

    public IReadOnlyList<MiddlewareBinding> Middlewares { get; }

    public IReadOnlyList<PolicyCommandBinding> Commands { get; }
}

public sealed class PolicyCommandBinding
{
    public PolicyCommandBinding(Type commandType)
    {
        CommandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
    }

    public Type CommandType { get; }
}
