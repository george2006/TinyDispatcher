#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Extraction;

internal sealed class ReferencedAssemblyContributionExtractor
{
    private const string ContextContributionAttributeName = "TinyDispatcher.TinyDispatcherAssemblyContextContributionAttribute";
    private const string HandlerContributionAttributeName = "TinyDispatcher.TinyDispatcherHandlerContributionAttribute";
    private const string PipelineContributionAttributeName = "TinyDispatcher.TinyDispatcherPipelineContributionAttribute";
    private const string PolicyContributionAttributeName = "TinyDispatcher.TinyDispatcherPolicyContributionAttribute";

    public ReferencedAssemblyContributions Extract(Compilation compilation)
    {
        if (compilation is null) throw new ArgumentNullException(nameof(compilation));

        var contextAttribute = compilation.GetTypeByMetadataName(ContextContributionAttributeName);
        var handlerAttribute = compilation.GetTypeByMetadataName(HandlerContributionAttributeName);
        var pipelineAttribute = compilation.GetTypeByMetadataName(PipelineContributionAttributeName);
        var policyAttribute = compilation.GetTypeByMetadataName(PolicyContributionAttributeName);
        if (contextAttribute is null || handlerAttribute is null || pipelineAttribute is null || policyAttribute is null)
            return ReferencedAssemblyContributions.Empty;

        var assemblies = ImmutableArray.CreateBuilder<ReferencedAssemblyContribution>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            var handlers = ImmutableArray.CreateBuilder<HandlerContract>();
            string? contextTypeFqn = null;
            var perCommand = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(StringComparer.Ordinal);
            var policies = ImmutableDictionary.CreateBuilder<string, PolicySpec>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var attribute in assembly.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, contextAttribute))
                {
                    TryReadContext(attribute, ref contextTypeFqn);
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, handlerAttribute))
                {
                    if (!TryReadHandler(attribute, out var handler))
                        continue;

                    var key = handler.MessageTypeFqn + "|" + handler.HandlerTypeFqn + "|" + handler.ContextTypeFqn;
                    if (seen.Add(key))
                        handlers.Add(handler);

                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, pipelineAttribute))
                {
                    if (!TryReadPipeline(attribute, out var commandTypeFqn, out var middlewares))
                        continue;

                    if (commandTypeFqn is not null && middlewares.Length > 0)
                        perCommand[commandTypeFqn] = middlewares;

                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, policyAttribute) &&
                    TryReadPolicy(attribute, out var policy))
                {
                    policies[policy.PolicyTypeFqn] = policy;
                }
            }

            var contribution = new ReferencedAssemblyContribution(
                assembly.Identity.Name,
                contextTypeFqn,
                handlers.ToImmutable(),
                perCommand.ToImmutable(),
                policies.ToImmutable());

            if (contribution.HasContributions())
                assemblies.Add(contribution);
        }

        return assemblies.Count == 0
            ? ReferencedAssemblyContributions.Empty
            : new ReferencedAssemblyContributions(assemblies.ToImmutable());
    }

    private static bool TryReadContext(AttributeData attribute, ref string? contextTypeFqn)
    {
        if (attribute.ConstructorArguments.Length != 1)
            return false;

        if (!TryReadType(attribute.ConstructorArguments[0], out var contextType))
            return false;

        contextTypeFqn ??= contextType;
        return true;
    }

    private static bool TryReadHandler(AttributeData attribute, out HandlerContract handler)
    {
        handler = default!;

        if (attribute.ConstructorArguments.Length != 3)
            return false;

        if (!TryReadType(attribute.ConstructorArguments[0], out var commandType) ||
            !TryReadType(attribute.ConstructorArguments[1], out var handlerType) ||
            !TryReadType(attribute.ConstructorArguments[2], out var contextType))
        {
            return false;
        }

        handler = new HandlerContract(
            MessageTypeFqn: commandType,
            HandlerTypeFqn: handlerType,
            ContextTypeFqn: contextType);

        return true;
    }

    private static bool TryReadPipeline(
        AttributeData attribute,
        out string? commandTypeFqn,
        out ImmutableArray<MiddlewareRef> middlewares)
    {
        commandTypeFqn = null;
        middlewares = ImmutableArray<MiddlewareRef>.Empty;

        if (attribute.ConstructorArguments.Length != 1)
            return false;

        middlewares = ReadMiddlewareArray(attribute.ConstructorArguments[0]);

        foreach (var named in attribute.NamedArguments)
        {
            if (named.Key == "CommandType" &&
                named.Value.Kind == TypedConstantKind.Type &&
                named.Value.Value is ITypeSymbol typeSymbol)
            {
                commandTypeFqn = EnsureGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            }
        }

        return true;
    }

    private static bool TryReadPolicy(AttributeData attribute, out PolicySpec policy)
    {
        policy = default!;

        if (attribute.ConstructorArguments.Length != 3)
            return false;

        if (!TryReadType(attribute.ConstructorArguments[0], out var policyTypeFqn))
            return false;

        policy = new PolicySpec(
            PolicyTypeFqn: policyTypeFqn,
            Middlewares: ReadMiddlewareArray(attribute.ConstructorArguments[1]),
            Commands: ReadTypeArray(attribute.ConstructorArguments[2]));

        return true;
    }

    private static ImmutableArray<MiddlewareRef> ReadMiddlewareArray(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array || constant.Values.IsDefaultOrEmpty)
            return ImmutableArray<MiddlewareRef>.Empty;

        var builder = ImmutableArray.CreateBuilder<MiddlewareRef>(constant.Values.Length);

        foreach (var value in constant.Values)
        {
            if (value.Kind != TypedConstantKind.Type || value.Value is not INamedTypeSymbol typeSymbol)
                continue;

            builder.Add(MiddlewareRefFactory.Create(typeSymbol.OriginalDefinition));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<string> ReadTypeArray(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array || constant.Values.IsDefaultOrEmpty)
            return ImmutableArray<string>.Empty;

        var builder = ImmutableArray.CreateBuilder<string>(constant.Values.Length);

        foreach (var value in constant.Values)
        {
            if (value.Kind != TypedConstantKind.Type || value.Value is not ITypeSymbol typeSymbol)
                continue;

            builder.Add(EnsureGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return builder.ToImmutable();
    }

    private static bool TryReadType(TypedConstant constant, out string typeFqn)
    {
        typeFqn = string.Empty;

        if (constant.Kind != TypedConstantKind.Type || constant.Value is not ITypeSymbol typeSymbol)
            return false;

        typeFqn = EnsureGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        return true;
    }

    private static string EnsureGlobal(string value)
        => value.StartsWith("global::", StringComparison.Ordinal) ? value : "global::" + value;
}
