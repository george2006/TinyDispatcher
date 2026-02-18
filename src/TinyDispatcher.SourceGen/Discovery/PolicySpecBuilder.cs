#nullable enable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class PolicySpecBuilder
{
    public ImmutableDictionary<string, PolicySpec> Build(
        Compilation compilation,
        List<INamedTypeSymbol> policies)
    {
        if (policies is null || policies.Count == 0)
            return ImmutableDictionary<string, PolicySpec>.Empty;

        var distinct = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var p in policies)
        {
            var key = Fqn.EnsureGlobal(
                p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            if (!distinct.ContainsKey(key))
                distinct[key] = p;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, PolicySpec>(StringComparer.Ordinal);

        foreach (var kv in distinct)
        {
            var policyTypeFqn = kv.Key;
            var policy = kv.Value;

            if (!HasAttribute(policy, "TinyDispatcher.TinyPolicyAttribute"))
                continue;

            var mids = new List<MiddlewareRef>();
            var commands = new List<string>();

            foreach (var attr in policy.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString() ?? string.Empty;

                if (attrName == "TinyDispatcher.UseMiddlewareAttribute")
                {
                    if (TryGetTypeofArg(attr, out var mwType) && mwType is INamedTypeSymbol mwNamed)
                    {
                        mids.Add(CreateMiddlewareRef(mwNamed));
                    }
                }
                else if (attrName == "TinyDispatcher.ForCommandAttribute")
                {
                    if (TryGetTypeofArg(attr, out var cmdType) && cmdType is not null)
                    {
                        var cmdFqn = Fqn.EnsureGlobal(
                            cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                        commands.Add(cmdFqn);
                    }
                }
            }

            var seenMw = new HashSet<MiddlewareRef>();
            var midsDistinct = mids.Where(x => seenMw.Add(x)).ToImmutableArray();

            var seenCmd = new HashSet<string>(StringComparer.Ordinal);
            var cmdsDistinct = commands.Where(x => seenCmd.Add(x)).ToImmutableArray();

            if (midsDistinct.Length == 0 || cmdsDistinct.Length == 0)
                continue;

            builder[policyTypeFqn] = new PolicySpec(
                PolicyTypeFqn: policyTypeFqn,
                Middlewares: midsDistinct,
                Commands: cmdsDistinct);
        }

        return builder.ToImmutable();
    }

    private static MiddlewareRef CreateMiddlewareRef(INamedTypeSymbol middlewareType)
    {
        var open = middlewareType.OriginalDefinition;

        var fqnWithArgs = Fqn.EnsureGlobal(
            open.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        var baseFqn = StripGenericSuffix(fqnWithArgs);

        return new MiddlewareRef(open, baseFqn, open.Arity);
    }

    private static string StripGenericSuffix(string fqn)
    {
        var idx = fqn.IndexOf('<');
        return idx < 0 ? fqn : fqn.Substring(0, idx);
    }

    private static bool HasAttribute(INamedTypeSymbol symbol, string fullName)
    {
        return symbol.GetAttributes()
            .Any(a => string.Equals(
                a.AttributeClass?.ToDisplayString(),
                fullName,
                StringComparison.Ordinal));
    }

    private static bool TryGetTypeofArg(AttributeData attr, out ITypeSymbol? type)
    {
        type = null;

        if (attr.ConstructorArguments.Length == 0)
            return false;

        var arg = attr.ConstructorArguments[0];

        if (arg.Kind == TypedConstantKind.Type && arg.Value is ITypeSymbol ts)
        {
            type = ts;
            return true;
        }

        return false;
    }
}
