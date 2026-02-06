using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Discovery;

internal sealed class PolicySpecBuilder
{
    private readonly MiddlewareRefFactory _mwFactory;

    public PolicySpecBuilder(MiddlewareRefFactory mwFactory)
        => _mwFactory = mwFactory ?? throw new ArgumentNullException(nameof(mwFactory));

    public ImmutableDictionary<string, PipelineEmitter.PolicySpec> Build(
        Compilation compilation,
        string expectedContextFqn,
        List<INamedTypeSymbol> policies,
        List<Diagnostic> diags)
    {
        if (policies is null || policies.Count == 0)
            return ImmutableDictionary<string, PipelineEmitter.PolicySpec>.Empty;

        // Distinct policy symbols
        var distinct = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var p in policies)
        {
            var key = Fqn.EnsureGlobal(p.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (!distinct.ContainsKey(key))
                distinct[key] = p;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, PipelineEmitter.PolicySpec>(StringComparer.Ordinal);

        foreach (var kv in distinct)
        {
            var policyTypeFqn = kv.Key;
            var policy = kv.Value;

            // Must have [TinyPolicy]
            if (!HasAttribute(policy, "TinyDispatcher.TinyPolicyAttribute"))
                continue;

            // Extract [UseMiddleware(typeof(Mw<>/Mw<,>))] and [ForCommand(typeof(Cmd))]
            var mids = new List<MiddlewareRef>();
            var commands = new List<string>();

            foreach (var attr in policy.GetAttributes())
            {
                var attrName = attr.AttributeClass?.ToDisplayString() ?? string.Empty;

                if (attrName == "TinyDispatcher.UseMiddlewareAttribute")
                {
                    if (TryGetTypeofArg(attr, out var mwType) && mwType is INamedTypeSymbol mwNamed)
                    {
                        var open = mwNamed.OriginalDefinition;

                        if (!_mwFactory.TryCreate(compilation, open, expectedContextFqn, out var mwRef, out var diag))
                        {
                            if (diag != null) diags.Add(diag);
                            continue;
                        }

                        mids.Add(mwRef);
                    }
                }
                else if (attrName == "TinyDispatcher.ForCommandAttribute")
                {
                    if (TryGetTypeofArg(attr, out var cmdType) && cmdType != null)
                    {
                        var cmdFqn = Fqn.EnsureGlobal(cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        commands.Add(cmdFqn);
                    }
                }
            }

            // Distinct middleware (keep declared order)
            var seenMw = new HashSet<MiddlewareRef>();
            var midsDistinct = mids.Where(x => seenMw.Add(x)).ToImmutableArray();

            // Distinct commands (keep declared order)
            var seenCmd = new HashSet<string>(StringComparer.Ordinal);
            var cmdsDistinct = commands.Where(x => seenCmd.Add(x)).ToImmutableArray();

            if (midsDistinct.Length == 0 || cmdsDistinct.Length == 0)
                continue;

            builder[policyTypeFqn] = new PipelineEmitter.PolicySpec(
                PolicyTypeFqn: policyTypeFqn,
                Middlewares: midsDistinct,
                Commands: cmdsDistinct);
        }

        return builder.ToImmutable();
    }

    private static bool HasAttribute(INamedTypeSymbol symbol, string fullName)
    {
        foreach (var a in symbol.GetAttributes())
        {
            var name = a.AttributeClass?.ToDisplayString() ?? string.Empty;
            if (string.Equals(name, fullName, StringComparison.Ordinal))
                return true;
        }
        return false;
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
