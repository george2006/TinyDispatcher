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
        if (compilation is null)
        {
            throw new ArgumentNullException(nameof(compilation));
        }

        if (!TryCreateAttributeSet(compilation, out var attributeSet))
        {
            return ReferencedAssemblyContributions.Empty;
        }

        var assemblies = ImmutableArray.CreateBuilder<ReferencedAssemblyContribution>();

        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
        {
            if (TryExtractAssemblyContribution(
                assembly,
                attributeSet,
                out var contribution))
            {
                assemblies.Add(contribution);
            }
        }

        if (assemblies.Count == 0)
        {
            return ReferencedAssemblyContributions.Empty;
        }

        return new ReferencedAssemblyContributions(assemblies.ToImmutable());
    }

    private static bool TryCreateAttributeSet(Compilation compilation, out AttributeSet attributeSet)
    {
        var resolvedContextAttribute = compilation.GetTypeByMetadataName(ContextContributionAttributeName);
        var resolvedHandlerAttribute = compilation.GetTypeByMetadataName(HandlerContributionAttributeName);
        var resolvedPipelineAttribute = compilation.GetTypeByMetadataName(PipelineContributionAttributeName);
        var resolvedPolicyAttribute = compilation.GetTypeByMetadataName(PolicyContributionAttributeName);

        if (resolvedContextAttribute is null ||
            resolvedHandlerAttribute is null ||
            resolvedPipelineAttribute is null ||
            resolvedPolicyAttribute is null)
        {
            attributeSet = default;
            return false;
        }

        attributeSet = new AttributeSet(
            resolvedContextAttribute,
            resolvedHandlerAttribute,
            resolvedPipelineAttribute,
            resolvedPolicyAttribute);
        return true;
    }

    private static bool TryExtractAssemblyContribution(
        IAssemblySymbol assembly,
        AttributeSet attributeSet,
        out ReferencedAssemblyContribution contribution)
    {
        var state = new ContributionState();

        foreach (var attribute in assembly.GetAttributes())
        {
            ProcessAttribute(
                attribute,
                attributeSet,
                state);
        }

        contribution = state.Build(assembly.Identity.Name);
        return contribution.HasContributions();
    }

    private static void ProcessAttribute(
        AttributeData attribute,
        AttributeSet attributeSet,
        ContributionState state)
    {
        if (IsContextAttribute(attribute, attributeSet))
        {
            AddContext(attribute, state);
            return;
        }

        if (IsHandlerAttribute(attribute, attributeSet))
        {
            AddHandler(attribute, state);
            return;
        }

        if (IsPipelineAttribute(attribute, attributeSet))
        {
            AddPipeline(attribute, state);
            return;
        }

        if (IsPolicyAttribute(attribute, attributeSet))
        {
            AddPolicy(attribute, state);
        }
    }

    private static bool IsContextAttribute(AttributeData attribute, AttributeSet attributeSet)
    {
        return MatchesAttribute(attribute, attributeSet.ContextAttribute);
    }

    private static bool IsHandlerAttribute(AttributeData attribute, AttributeSet attributeSet)
    {
        return MatchesAttribute(attribute, attributeSet.HandlerAttribute);
    }

    private static bool IsPipelineAttribute(AttributeData attribute, AttributeSet attributeSet)
    {
        return MatchesAttribute(attribute, attributeSet.PipelineAttribute);
    }

    private static bool IsPolicyAttribute(AttributeData attribute, AttributeSet attributeSet)
    {
        return MatchesAttribute(attribute, attributeSet.PolicyAttribute);
    }

    private static bool MatchesAttribute(AttributeData attribute, INamedTypeSymbol expectedAttribute)
    {
        return SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, expectedAttribute);
    }

    private static void AddContext(AttributeData attribute, ContributionState state)
    {
        var contextTypeFqn = state.ContextTypeFqn;
        if (!TryReadContext(attribute, ref contextTypeFqn))
        {
            return;
        }

        state.ContextTypeFqn = contextTypeFqn;
    }

    private static bool TryReadContext(AttributeData attribute, ref string? contextTypeFqn)
    {
        if (attribute.ConstructorArguments.Length != 1)
        {
            return false;
        }

        if (!TryReadType(attribute.ConstructorArguments[0], out var contextType))
        {
            return false;
        }

        contextTypeFqn ??= contextType;
        return true;
    }

    private static void AddHandler(AttributeData attribute, ContributionState state)
    {
        if (!TryReadHandler(attribute, out var handler))
        {
            return;
        }

        var handlerKey = CreateHandlerKey(handler);
        if (state.SeenHandlers.Add(handlerKey))
        {
            state.Handlers.Add(handler);
        }
    }

    private static string CreateHandlerKey(HandlerContract handler)
    {
        return handler.MessageTypeFqn + "|" + handler.HandlerTypeFqn + "|" + handler.ContextTypeFqn;
    }

    private static bool TryReadHandler(AttributeData attribute, out HandlerContract handler)
    {
        handler = default!;

        if (attribute.ConstructorArguments.Length != 3)
        {
            return false;
        }

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

    private static void AddPipeline(AttributeData attribute, ContributionState state)
    {
        if (!TryReadPipeline(attribute, out var commandTypeFqn, out var middlewares))
        {
            return;
        }

        if (!HasPipelineContribution(commandTypeFqn, middlewares))
        {
            return;
        }

        state.Pipelines[commandTypeFqn!] = middlewares;
    }

    private static bool HasPipelineContribution(string? commandTypeFqn, ImmutableArray<MiddlewareRef> middlewares)
    {
        return commandTypeFqn is not null && middlewares.Length > 0;
    }

    private static bool TryReadPipeline(
        AttributeData attribute,
        out string? commandTypeFqn,
        out ImmutableArray<MiddlewareRef> middlewares)
    {
        commandTypeFqn = null;
        middlewares = ImmutableArray<MiddlewareRef>.Empty;

        if (attribute.ConstructorArguments.Length != 1)
        {
            return false;
        }

        middlewares = ReadMiddlewareArray(attribute.ConstructorArguments[0]);
        commandTypeFqn = ReadPipelineCommandType(attribute);

        return true;
    }

    private static string? ReadPipelineCommandType(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments)
        {
            if (TryReadPipelineCommandType(namedArgument, out var commandTypeFqn))
            {
                return commandTypeFqn;
            }
        }

        return null;
    }

    private static bool TryReadPipelineCommandType(
        KeyValuePair<string, TypedConstant> namedArgument,
        out string commandTypeFqn)
    {
        commandTypeFqn = string.Empty;

        if (namedArgument.Key != "CommandType")
        {
            return false;
        }

        if (!TryReadType(namedArgument.Value, out commandTypeFqn))
        {
            return false;
        }

        return true;
    }

    private static ImmutableArray<MiddlewareRef> ReadMiddlewareArray(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array || constant.Values.IsDefaultOrEmpty)
        {
            return ImmutableArray<MiddlewareRef>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<MiddlewareRef>(constant.Values.Length);

        foreach (var value in constant.Values)
        {
            if (!TryReadNamedType(value, out var typeSymbol))
            {
                continue;
            }

            builder.Add(MiddlewareRefFactory.Create(typeSymbol.OriginalDefinition));
        }

        return builder.ToImmutable();
    }

    private static void AddPolicy(AttributeData attribute, ContributionState state)
    {
        if (TryReadPolicy(attribute, out var policy))
        {
            state.Policies[policy.PolicyTypeFqn] = policy;
        }
    }

    private static bool TryReadPolicy(AttributeData attribute, out PolicySpec policy)
    {
        policy = default!;

        if (attribute.ConstructorArguments.Length != 3)
        {
            return false;
        }

        if (!TryReadType(attribute.ConstructorArguments[0], out var policyTypeFqn))
        {
            return false;
        }

        policy = new PolicySpec(
            PolicyTypeFqn: policyTypeFqn,
            Middlewares: ReadMiddlewareArray(attribute.ConstructorArguments[1]),
            Commands: ReadTypeArray(attribute.ConstructorArguments[2]));

        return true;
    }

    private static ImmutableArray<string> ReadTypeArray(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array || constant.Values.IsDefaultOrEmpty)
        {
            return ImmutableArray<string>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<string>(constant.Values.Length);

        foreach (var value in constant.Values)
        {
            if (!TryReadAnyType(value, out var typeSymbol))
            {
                continue;
            }

            builder.Add(EnsureGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        return builder.ToImmutable();
    }

    private static bool TryReadType(TypedConstant constant, out string typeFqn)
    {
        typeFqn = string.Empty;

        if (!TryReadAnyType(constant, out var typeSymbol))
        {
            return false;
        }

        typeFqn = EnsureGlobal(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        return true;
    }

    private static bool TryReadNamedType(TypedConstant constant, out INamedTypeSymbol typeSymbol)
    {
        typeSymbol = default!;

        if (constant.Kind != TypedConstantKind.Type || constant.Value is not INamedTypeSymbol namedTypeSymbol)
        {
            return false;
        }

        typeSymbol = namedTypeSymbol;
        return true;
    }

    private static bool TryReadAnyType(TypedConstant constant, out ITypeSymbol typeSymbol)
    {
        typeSymbol = default!;

        if (constant.Kind != TypedConstantKind.Type || constant.Value is not ITypeSymbol resolvedTypeSymbol)
        {
            return false;
        }

        typeSymbol = resolvedTypeSymbol;
        return true;
    }

    private static string EnsureGlobal(string value)
    {
        if (value.StartsWith("global::", StringComparison.Ordinal))
        {
            return value;
        }

        return "global::" + value;
    }

    private readonly record struct AttributeSet(
        INamedTypeSymbol ContextAttribute,
        INamedTypeSymbol HandlerAttribute,
        INamedTypeSymbol PipelineAttribute,
        INamedTypeSymbol PolicyAttribute);

    private sealed class ContributionState
    {
        public ImmutableArray<HandlerContract>.Builder Handlers { get; } = ImmutableArray.CreateBuilder<HandlerContract>();

        public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Builder Pipelines { get; }
            = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(StringComparer.Ordinal);

        public ImmutableDictionary<string, PolicySpec>.Builder Policies { get; }
            = ImmutableDictionary.CreateBuilder<string, PolicySpec>(StringComparer.Ordinal);

        public HashSet<string> SeenHandlers { get; } = new(StringComparer.Ordinal);

        public string? ContextTypeFqn { get; set; }

        public ReferencedAssemblyContribution Build(string assemblyName)
        {
            return new ReferencedAssemblyContribution(
                assemblyName,
                ContextTypeFqn,
                Handlers.ToImmutable(),
                Pipelines.ToImmutable(),
                Policies.ToImmutable());
        }
    }
}
