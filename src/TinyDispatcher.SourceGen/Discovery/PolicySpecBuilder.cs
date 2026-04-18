#nullable enable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class PolicySpecBuilder
{
    private const string TinyPolicyAttributeName = "TinyDispatcher.TinyPolicyAttribute";
    private const string UseMiddlewareAttributeName = "TinyDispatcher.UseMiddlewareAttribute";
    private const string ForCommandAttributeName = "TinyDispatcher.ForCommandAttribute";

    public ImmutableDictionary<string, PolicySpec> Build(List<INamedTypeSymbol> policies)
    {
        if (policies is null || policies.Count == 0)
        {
            return ImmutableDictionary<string, PolicySpec>.Empty;
        }

        var distinctPolicies = GetDistinctPolicies(policies);

        return BuildPolicySpecs(distinctPolicies);
    }

    private static Dictionary<string, INamedTypeSymbol> GetDistinctPolicies(List<INamedTypeSymbol> policies)
    {
        var distinctPolicies = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        for (var i = 0; i < policies.Count; i++)
        {
            var p = policies[i];
            var key = Fqn.FromType(p);

            var policyWasAlreadySeen = distinctPolicies.ContainsKey(key);
            if (policyWasAlreadySeen)
            {
                continue;
            }

            distinctPolicies[key] = p;
        }

        return distinctPolicies;
    }

    private static ImmutableDictionary<string, PolicySpec> BuildPolicySpecs(
        Dictionary<string, INamedTypeSymbol> policiesByTypeName)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PolicySpec>(StringComparer.Ordinal);

        foreach (var pair in policiesByTypeName)
        {
            var policyTypeFqn = pair.Key;
            var policy = pair.Value;
            var spec = BuildPolicySpecOrNull(policyTypeFqn, policy);

            if (spec is null)
            {
                continue;
            }

            builder[policyTypeFqn] = spec;
        }

        return builder.ToImmutable();
    }

    private static PolicySpec? BuildPolicySpecOrNull(
        string policyTypeFqn,
        INamedTypeSymbol policy)
    {
        var hasPolicyAttribute = HasAttribute(policy, TinyPolicyAttributeName);
        if (!hasPolicyAttribute)
        {
            return null;
        }

        var middlewares = new List<MiddlewareRef>();
        var commands = new List<string>();

        ReadPolicyAttributes(policy, middlewares, commands);

        var distinctMiddlewares = DistinctMiddlewares(middlewares);
        var distinctCommands = DistinctCommands(commands);
        var policyIsIncomplete = distinctMiddlewares.Length == 0 || distinctCommands.Length == 0;

        if (policyIsIncomplete)
        {
            return null;
        }

        return new PolicySpec(
            PolicyTypeFqn: policyTypeFqn,
            Middlewares: distinctMiddlewares,
            Commands: distinctCommands);
    }

    private static void ReadPolicyAttributes(
        INamedTypeSymbol policy,
        List<MiddlewareRef> middlewares,
        List<string> commands)
    {
        foreach (var attribute in policy.GetAttributes())
        {
            ReadPolicyAttribute(attribute, middlewares, commands);
        }
    }

    private static void ReadPolicyAttribute(
        AttributeData attribute,
        List<MiddlewareRef> middlewares,
        List<string> commands)
    {
        var attributeName = attribute.AttributeClass?.ToDisplayString() ?? string.Empty;
        var isMiddlewareAttribute = attributeName == UseMiddlewareAttributeName;

        if (isMiddlewareAttribute)
        {
            AddMiddlewareIfPresent(attribute, middlewares);
            return;
        }

        var isCommandAttribute = attributeName == ForCommandAttributeName;

        if (isCommandAttribute)
        {
            AddCommandIfPresent(attribute, commands);
        }
    }

    private static void AddMiddlewareIfPresent(AttributeData attribute, List<MiddlewareRef> middlewares)
    {
        if (!TryGetTypeofArg(attribute, out var middlewareType))
        {
            return;
        }

        if (middlewareType is not INamedTypeSymbol middlewareNamedType)
        {
            return;
        }

        middlewares.Add(MiddlewareRefFactory.Create(middlewareNamedType));
    }

    private static void AddCommandIfPresent(AttributeData attribute, List<string> commands)
    {
        if (!TryGetTypeofArg(attribute, out var commandType))
        {
            return;
        }

        if (commandType is null)
        {
            return;
        }

        var commandFqn = Fqn.FromType(commandType);

        commands.Add(commandFqn);
    }

    private static bool HasAttribute(INamedTypeSymbol symbol, string fullName)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.ToDisplayString();
            if (string.Equals(attributeName, fullName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<MiddlewareRef> DistinctMiddlewares(List<MiddlewareRef> middlewares)
    {
        if (middlewares.Count == 0)
            return ImmutableArray<MiddlewareRef>.Empty;

        var seen = new HashSet<MiddlewareRef>();
        var builder = ImmutableArray.CreateBuilder<MiddlewareRef>(middlewares.Count);

        for (var i = 0; i < middlewares.Count; i++)
        {
            var middleware = middlewares[i];
            var isFirstOccurrence = seen.Add(middleware);

            if (isFirstOccurrence)
            {
                builder.Add(middleware);
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> DistinctCommands(List<string> commands)
    {
        if (commands.Count == 0)
            return ImmutableArray<string>.Empty;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<string>(commands.Count);

        for (var i = 0; i < commands.Count; i++)
        {
            var command = commands[i];
            var isFirstOccurrence = seen.Add(command);

            if (isFirstOccurrence)
            {
                builder.Add(command);
            }
        }

        return builder.ToImmutable();
    }

    private static bool TryGetTypeofArg(AttributeData attr, out ITypeSymbol? type)
    {
        type = null;

        if (attr.ConstructorArguments.Length == 0)
            return false;

        var arg = attr.ConstructorArguments[0];

        var argumentIsType = arg.Kind == TypedConstantKind.Type;
        if (argumentIsType && arg.Value is ITypeSymbol ts)
        {
            type = ts;
            return true;
        }

        return false;
    }
}
