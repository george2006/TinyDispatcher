using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen
{
    /// <summary>
    /// Uses Roslyn to discover TinyDispatcher command and query handlers in a given compilation.
    /// </summary>
    internal sealed class RoslynHandlerDiscovery : IHandlerDiscovery
    {
        private readonly string _coreNamespace;
        private readonly string? _namespacePrefixFilter;
        private readonly string? _commandContextTypeFqn; // global::...

        public RoslynHandlerDiscovery(string coreNamespace, string? includeNamespacePrefix, string? commandContextTypeFqn)
        {
            _coreNamespace = coreNamespace;
            _namespacePrefixFilter = string.IsNullOrWhiteSpace(includeNamespacePrefix) ? null : includeNamespacePrefix;
            _commandContextTypeFqn = string.IsNullOrWhiteSpace(commandContextTypeFqn) ? null : EnsureGlobalPrefix(commandContextTypeFqn!);
        }

        public DiscoveryResult Discover(Compilation compilation, ImmutableArray<INamedTypeSymbol> candidates)
        {
            var (commandHandlerInterface, queryHandlerInterface) = ResolveHandlerInterfaces(compilation);

            if (commandHandlerInterface is null || queryHandlerInterface is null)
                return DiscoveryResultExtensions.Empty;

            var commandHandlers = ImmutableArray.CreateBuilder<HandlerContract>();
            var queryHandlers = ImmutableArray.CreateBuilder<QueryHandlerContract>();

            foreach (var candidate in candidates)
            {
                if (!IsConcreteHandlerType(candidate))
                    continue;

                if (!IsIncludedByNamespaceFilter(candidate))
                    continue;

                CollectHandlerContracts(
                    candidate,
                    commandHandlerInterface,
                    queryHandlerInterface,
                    commandHandlers,
                    queryHandlers);
            }

            return new DiscoveryResult(commandHandlers.ToImmutable(), queryHandlers.ToImmutable());
        }

        private (INamedTypeSymbol? commandHandler, INamedTypeSymbol? queryHandler) ResolveHandlerInterfaces(Compilation compilation)
        {
            // ✅ UPDATED: ICommandHandler`2 (TCommand, TContext)
            var commandHandler = compilation.GetTypeByMetadataName($"{_coreNamespace}.ICommandHandler`2");
            var queryHandler = compilation.GetTypeByMetadataName($"{_coreNamespace}.IQueryHandler`2");
            return (commandHandler, queryHandler);
        }

        private static bool IsConcreteHandlerType(INamedTypeSymbol? type)
            => type is not null && type.TypeKind == TypeKind.Class && !type.IsAbstract;

        private bool IsIncludedByNamespaceFilter(INamedTypeSymbol type)
        {
            if (_namespacePrefixFilter is null)
                return true;

            var ns = type.ContainingNamespace?.ToDisplayString();
            if (string.IsNullOrEmpty(ns))
                return false;

            return ns!.StartsWith(_namespacePrefixFilter, StringComparison.Ordinal);
        }

        private void CollectHandlerContracts(
            INamedTypeSymbol candidate,
            INamedTypeSymbol commandHandlerInterface,
            INamedTypeSymbol queryHandlerInterface,
            ImmutableArray<HandlerContract>.Builder commandHandlers,
            ImmutableArray<QueryHandlerContract>.Builder queryHandlers)
        {
            foreach (var implementedInterface in candidate.AllInterfaces)
            {
                var openInterface = implementedInterface.OriginalDefinition;

                if (SymbolEqualityComparer.Default.Equals(openInterface, commandHandlerInterface)
                    && implementedInterface.TypeArguments.Length == 2)
                {
                    AddCommandHandlerContract(candidate, implementedInterface, commandHandlers);
                }
                else if (SymbolEqualityComparer.Default.Equals(openInterface, queryHandlerInterface)
                         && implementedInterface.TypeArguments.Length == 2)
                {
                    AddQueryHandlerContract(candidate, implementedInterface, queryHandlers);
                }
            }
        }

        private void AddCommandHandlerContract(
            INamedTypeSymbol handlerType,
            INamedTypeSymbol interfaceType,
            ImmutableArray<HandlerContract>.Builder commandHandlers)
        {
            var commandArg = interfaceType.TypeArguments[0];
            var ctxArg = interfaceType.TypeArguments[1];

            if (commandArg.TypeKind == TypeKind.TypeParameter || ctxArg.TypeKind == TypeKind.TypeParameter)
                return;

            if (_commandContextTypeFqn is not null)
            {
                var ctxFqn = EnsureGlobalPrefix(ctxArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                if (!string.Equals(ctxFqn, _commandContextTypeFqn, StringComparison.Ordinal))
                    return;
            }

            var messageTypeFqn = EnsureGlobalPrefix(commandArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            var handlerTypeFqn = EnsureGlobalPrefix(handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            commandHandlers.Add(new HandlerContract(messageTypeFqn, handlerTypeFqn));
        }

        private static void AddQueryHandlerContract(
            INamedTypeSymbol handlerType,
            INamedTypeSymbol interfaceType,
            ImmutableArray<QueryHandlerContract>.Builder queryHandlers)
        {
            var queryArg = interfaceType.TypeArguments[0];
            var resultArg = interfaceType.TypeArguments[1];

            if (queryArg.TypeKind == TypeKind.TypeParameter || resultArg.TypeKind == TypeKind.TypeParameter)
                return;

            var queryTypeFqn = queryArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var resultTypeFqn = resultArg.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var handlerTypeFqn = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            queryHandlers.Add(new QueryHandlerContract(queryTypeFqn, resultTypeFqn, handlerTypeFqn));
        }

        private static string EnsureGlobalPrefix(string s)
            => s.StartsWith("global::", StringComparison.Ordinal) ? s : "global::" + s;
    }

    internal static class DiscoveryResultExtensions
    {
        public static DiscoveryResult Empty { get; } =
            new DiscoveryResult(
                ImmutableArray<HandlerContract>.Empty,
                ImmutableArray<QueryHandlerContract>.Empty);
    }
}
