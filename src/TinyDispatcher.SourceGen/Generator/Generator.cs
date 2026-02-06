// TinyDispatcher.SourceGen/Generator.cs
// -----------------------------------------------------------------------------
// TinyDispatcher incremental source generator (netstandard2.0 friendly)
//
// ALWAYS emits:
//  - DispatcherModuleInitializer
//  - ThisAssemblyContribution
//  - EmptyPipelineContribution
//
// Conditionally emits (HOST ARTIFACTS):
//  - TinyDispatcherPipeline.g.cs       (when middleware/policies are declared via TinyBootstrap)
//
// HOST GATE (NO HEURISTICS, NO MSBUILD FLAGS):
//  - Only emit host artifacts if we find at least one call to:
//      services.UseTinyDispatcher<TContext>(tiny => { ... })
//    Discovery is SYNTAX-based.
// -----------------------------------------------------------------------------

#nullable enable

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Internal;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator;

// ============================================================================
// Components (composed via `new` inside Execute; no static helpers for behavior)
// ============================================================================

internal sealed class UseTinyDispatcherSyntax
{
    public bool IsUseTinyDispatcherInvocation(InvocationExpressionSyntax inv)
    {
        // services.UseTinyDispatcher<TContext>(...)
        if (inv.Expression is MemberAccessExpressionSyntax ma)
        {
            if (ma.Name is GenericNameSyntax g &&
                string.Equals(g.Identifier.ValueText, "UseTinyDispatcher", StringComparison.Ordinal) &&
                g.TypeArgumentList != null &&
                g.TypeArgumentList.Arguments.Count == 1)
                return true;

            return false;
        }

        // UseTinyDispatcher<TContext>(...) (using static)
        if (inv.Expression is GenericNameSyntax gg &&
            string.Equals(gg.Identifier.ValueText, "UseTinyDispatcher", StringComparison.Ordinal) &&
            gg.TypeArgumentList != null &&
            gg.TypeArgumentList.Arguments.Count == 1)
            return true;

        return false;
    }
}

internal sealed class ContextInference
{
    public string? TryInferContextTypeFromUseTinyCalls(
        ImmutableArray<InvocationExpressionSyntax> calls,
        Compilation compilation)
    {
        for (var i = 0; i < calls.Length; i++)
        {
            var call = calls[i];

            GenericNameSyntax? g = null;

            if (call.Expression is MemberAccessExpressionSyntax ma)
                g = ma.Name as GenericNameSyntax;
            else
                g = call.Expression as GenericNameSyntax;

            if (g == null || g.TypeArgumentList == null || g.TypeArgumentList.Arguments.Count != 1)
                continue;

            var ctxTypeSyntax = g.TypeArgumentList.Arguments[0];

            var model = compilation.GetSemanticModel(call.SyntaxTree);
            var ctxType = model.GetTypeInfo(ctxTypeSyntax).Type;

            if (ctxType == null)
                continue;

            return Fqn.EnsureGlobal(ctxType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        }

        return null;
    }
}

internal sealed class MiddlewareRefFactory
{
    private readonly DiagnosticsCatalog _diags;

    public MiddlewareRefFactory(DiagnosticsCatalog diags)
        => _diags = diags ?? throw new ArgumentNullException(nameof(diags));

    public bool TryCreate(
        Compilation compilation,
        INamedTypeSymbol openMiddlewareType,
        string expectedContextFqn,
        out MiddlewareRef middleware,
        out Diagnostic? diagnostic)
    {
        middleware = default;
        diagnostic = null;

        if (!openMiddlewareType.IsGenericType || !openMiddlewareType.IsDefinition)
        {
            diagnostic = _diags.CreateError(
                "DISP301",
                "Invalid middleware type",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must be an open generic type definition (e.g. typeof(MyMiddleware<,>) or typeof(MyMiddleware<>)).");
            return false;
        }

        var arity = openMiddlewareType.Arity;

        if (arity != 1 && arity != 2)
        {
            diagnostic = _diags.CreateError(
                "DISP302",
                "Unsupported middleware arity",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must have arity 1 or 2.");
            return false;
        }

        var fmtNoGenerics = SymbolDisplayFormat.FullyQualifiedFormat
            .WithGenericsOptions(SymbolDisplayGenericsOptions.None);

        var openFqn = Fqn.EnsureGlobal(openMiddlewareType.ToDisplayString(fmtNoGenerics));

        if (arity == 2)
        {
            middleware = new MiddlewareRef(openFqn, 2);
            return true;
        }

        // Arity 1: must implement exactly one ICommandMiddleware<TCommand, TContextClosed>
        var iface = compilation.GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2");
        if (iface is null)
        {
            diagnostic = _diags.CreateError(
                "DISP303",
                "Cannot resolve ICommandMiddleware",
                "Could not resolve 'TinyDispatcher.ICommandMiddleware`2' from compilation.");
            return false;
        }

        var matches = 0;

        foreach (var i in openMiddlewareType.AllInterfaces)
        {
            if (!SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, iface))
                continue;

            if (i.TypeArguments.Length != 2)
                continue;

            // TCommand must be the middleware generic parameter #0
            if (i.TypeArguments[0] is not ITypeParameterSymbol tp || tp.Ordinal != 0)
                continue;

            // TContext must be CLOSED
            if (i.TypeArguments[1] is ITypeParameterSymbol)
                continue;

            var ctxFqn = Fqn.EnsureGlobal(i.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            if (!string.Equals(ctxFqn, expectedContextFqn, StringComparison.Ordinal))
                continue;

            matches++;
        }

        if (matches != 1)
        {
            diagnostic = _diags.CreateError(
                "DISP304",
                "Invalid context-closed middleware",
                $"Middleware '{openMiddlewareType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}' must implement exactly one ICommandMiddleware<TCommand, {expectedContextFqn}>.");
            return false;
        }

        middleware = new MiddlewareRef(openFqn, 1);
        return true;
    }
}

internal sealed class TinyBootstrapInvocationExtractor
{
    private readonly MiddlewareRefFactory _mwFactory;

    public TinyBootstrapInvocationExtractor(MiddlewareRefFactory mwFactory)
        => _mwFactory = mwFactory ?? throw new ArgumentNullException(nameof(mwFactory));

    public void Extract(
        InvocationExpressionSyntax useTinyCall,
        Compilation compilation,
        string expectedContextFqn,
        List<OrderedEntry> globals,
        List<OrderedPerCommandEntry> perCmd,
        List<INamedTypeSymbol> policies,
        List<Diagnostic> diags)
    {
        if (useTinyCall.ArgumentList is null)
            return;

        var lambda = useTinyCall.ArgumentList.Arguments
            .Select(a => a.Expression)
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();

        if (lambda is null)
            return;

        var model = compilation.GetSemanticModel(useTinyCall.SyntaxTree);

        IEnumerable<InvocationExpressionSyntax> invocations;
        if (lambda.Body is BlockSyntax block)
            invocations = block.DescendantNodes().OfType<InvocationExpressionSyntax>();
        else if (lambda.Body is ExpressionSyntax expr)
            invocations = expr.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();
        else
            invocations = Enumerable.Empty<InvocationExpressionSyntax>();

        foreach (var inv in invocations)
        {
            if (inv.Expression is not MemberAccessExpressionSyntax ma)
                continue;

            var methodName = ma.Name.Identifier.ValueText;

            // tiny.UseGlobalMiddleware(typeof(Mw<>/Mw<,>))
            if (string.Equals(methodName, "UseGlobalMiddleware", StringComparison.Ordinal))
            {
                var mwOpen = TryExtractOpenGenericTypeFromSingleTypeofArgument(inv, model);
                if (mwOpen is null) continue;

                if (!_mwFactory.TryCreate(compilation, mwOpen, expectedContextFqn, out var mwRef, out var diag))
                {
                    if (diag != null) diags.Add(diag);
                    continue;
                }

                globals.Add(new OrderedEntry(mwRef, OrderKey.From(inv)));
                continue;
            }

            // tiny.UseMiddlewareFor<TCommand>(typeof(Mw<>/Mw<,>)) OR tiny.UseMiddlewareFor(typeof(cmd), typeof(mw))
            if (string.Equals(methodName, "UseMiddlewareFor", StringComparison.Ordinal))
            {
                var genericName = ma.Name as GenericNameSyntax;
                if (genericName != null && genericName.TypeArgumentList.Arguments.Count == 1)
                {
                    var cmdTypeSyntax = genericName.TypeArgumentList.Arguments[0];
                    var cmdType = model.GetTypeInfo(cmdTypeSyntax).Type;
                    if (cmdType is null) continue;

                    var mwOpen = TryExtractOpenGenericTypeFromSingleTypeofArgument(inv, model);
                    if (mwOpen is null) continue;

                    if (!_mwFactory.TryCreate(compilation, mwOpen, expectedContextFqn, out var mwRef, out var diag))
                    {
                        if (diag != null) diags.Add(diag);
                        continue;
                    }

                    var cmdFqn = Fqn.EnsureGlobal(cmdType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                    perCmd.Add(new OrderedPerCommandEntry(cmdFqn, mwRef, OrderKey.From(inv)));
                    continue;
                }

                if (!TryExtractCommandAndMiddlewareFromTwoTypeofArguments(inv, model, out var cmdType2, out var mwOpen2))
                    continue;

                if (!_mwFactory.TryCreate(compilation, mwOpen2, expectedContextFqn, out var mwRef2, out var diag2))
                {
                    if (diag2 != null) diags.Add(diag2);
                    continue;
                }

                var cmdFqn2 = Fqn.EnsureGlobal(cmdType2.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                perCmd.Add(new OrderedPerCommandEntry(cmdFqn2, mwRef2, OrderKey.From(inv)));
                continue;
            }

            // tiny.UsePolicy<TPolicy>() OR services.UseTinyPolicy<TPolicy>() OR services.UseTinyPolicy(typeof(TPolicy))
            if (string.Equals(methodName, "UsePolicy", StringComparison.Ordinal) ||
                string.Equals(methodName, "UseTinyPolicy", StringComparison.Ordinal))
            {
                if (ma.Name is GenericNameSyntax g && g.TypeArgumentList.Arguments.Count == 1)
                {
                    var policyTypeSyntax = g.TypeArgumentList.Arguments[0];
                    var policyType = model.GetTypeInfo(policyTypeSyntax).Type as INamedTypeSymbol;
                    if (policyType is null) continue;

                    policies.Add(policyType);
                    continue;
                }

                if (inv.ArgumentList != null && inv.ArgumentList.Arguments.Count == 1)
                {
                    var toe = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
                    if (toe is null) continue;

                    var policyType = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
                    if (policyType is null) continue;

                    policies.Add(policyType);
                    continue;
                }
            }
        }
    }

    private static INamedTypeSymbol? TryExtractOpenGenericTypeFromSingleTypeofArgument(
        InvocationExpressionSyntax inv,
        SemanticModel model)
    {
        if (inv.ArgumentList is null || inv.ArgumentList.Arguments.Count != 1)
            return null;

        var toe = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
        if (toe is null)
            return null;

        var sym = model.GetSymbolInfo(toe.Type).Symbol as INamedTypeSymbol;
        return sym?.OriginalDefinition;
    }

    private static bool TryExtractCommandAndMiddlewareFromTwoTypeofArguments(
        InvocationExpressionSyntax inv,
        SemanticModel model,
        out ITypeSymbol commandType,
        out INamedTypeSymbol middlewareOpenType)
    {
        commandType = null!;
        middlewareOpenType = null!;

        if (inv.ArgumentList is null || inv.ArgumentList.Arguments.Count < 2)
            return false;

        var a0 = inv.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax;
        var a1 = inv.ArgumentList.Arguments[1].Expression as TypeOfExpressionSyntax;

        if (a0 is null || a1 is null)
            return false;

        var cmdSym = model.GetSymbolInfo(a0.Type).Symbol as ITypeSymbol;
        var mwSym = model.GetSymbolInfo(a1.Type).Symbol as INamedTypeSymbol;

        if (cmdSym is null || mwSym is null)
            return false;

        commandType = cmdSym;
        middlewareOpenType = mwSym.OriginalDefinition;
        return true;
    }
}

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

internal sealed class MiddlewareOrdering
{
    public ImmutableArray<MiddlewareRef> OrderAndDistinctGlobals(List<OrderedEntry> items)
    {
        var ordered = items
            .OrderBy(x => x.Order.FilePath, StringComparer.Ordinal)
            .ThenBy(x => x.Order.SpanStart);

        var seen = new HashSet<MiddlewareRef>();
        var list = new List<MiddlewareRef>();

        foreach (var x in ordered)
        {
            if (seen.Add(x.Middleware))
                list.Add(x.Middleware);
        }

        return list.ToImmutableArray();
    }

    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> BuildPerCommandMap(List<OrderedPerCommandEntry> items)
    {
        var ordered = items
            .OrderBy(x => x.Order.FilePath, StringComparer.Ordinal)
            .ThenBy(x => x.Order.SpanStart);

        var dict = new Dictionary<string, List<MiddlewareRef>>(StringComparer.Ordinal);

        foreach (var e in ordered)
        {
            if (!dict.TryGetValue(e.CommandFqn, out var list))
            {
                list = new List<MiddlewareRef>();
                dict[e.CommandFqn] = list;
            }

            list.Add(e.Middleware);
        }

        var builder = ImmutableDictionary.CreateBuilder<string, ImmutableArray<MiddlewareRef>>(StringComparer.Ordinal);

        foreach (var kv in dict)
        {
            var seen = new HashSet<MiddlewareRef>();
            var arr = kv.Value.Where(x => seen.Add(x)).ToImmutableArray();
            builder[kv.Key] = arr;
        }

        return builder.ToImmutable();
    }
}

// ============================================================================
// Public-ish shared model (used by PipelineEmitter signatures in this branch)
// ============================================================================

public readonly record struct MiddlewareRef(string OpenTypeFqn, int Arity);

// ============================================================================
// Generator
// ============================================================================

[Generator]
public sealed class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var syntax = new UseTinyDispatcherSyntax();

        // ---------------------------------------------------------------------
        // Handler candidates (anchor – always produces)
        // ---------------------------------------------------------------------
        var handlerCandidates =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (n, _) => n is ClassDeclarationSyntax,
                    static (ctx, ct) =>
                    {
                        var node = (ClassDeclarationSyntax)ctx.Node;
                        var model = ctx.SemanticModel;
                        return (INamedTypeSymbol?)model.GetDeclaredSymbol(node, ct);
                    })
                .Collect();

        // ---------------------------------------------------------------------
        // Find UseTinyDispatcher<TContext>(...) calls (SYNTAX-based)
        // IMPORTANT: do not rely on GetSymbolInfo here.
        // ---------------------------------------------------------------------
        var useTinyCalls =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    static (n, _) => n is InvocationExpressionSyntax,
                    (ctx, _) =>
                    {
                        var inv = (InvocationExpressionSyntax)ctx.Node;
                        return syntax.IsUseTinyDispatcherInvocation(inv) ? inv : null;
                    })
                .Collect();

        var pipeline =
            context.CompilationProvider
                .Combine(handlerCandidates)
                .Combine(useTinyCalls)
                .Combine(context.AnalyzerConfigOptionsProvider);

        context.RegisterSourceOutput(pipeline, Execute);
    }

    // =====================================================================
    // EXECUTE
    // =====================================================================

    private static void Execute(
        SourceProductionContext spc,
        (((Compilation Compilation,
           ImmutableArray<INamedTypeSymbol?> Handlers) Left,
          ImmutableArray<InvocationExpressionSyntax?> UseTinyCalls) Left,
         AnalyzerConfigOptionsProvider Options) data)
    {
        var compilation = data.Left.Left.Compilation;

        // Filter nullables (broken/partial code scenarios).
        var handlerSymbols = data.Left.Left.Handlers
            .Where(static s => s != null)
            .Select(static s => s!)
            .ToImmutableArray();

        var useTinyCalls = data.Left.UseTinyCalls
            .Where(static x => x != null)
            .Select(static x => x!)
            .ToImmutableArray();

        var roslynContext = new RoslynGeneratorContext(spc);

        // Compose components (explicit `new`, no DI container)
        var diagsCatalog = new DiagnosticsCatalog();
        var optionsFactory = new GeneratorOptionsFactory(new OptionsProvider());
        var ctxInference = new ContextInference();
        var mwFactory = new MiddlewareRefFactory(diagsCatalog);
        var extractor = new TinyBootstrapInvocationExtractor(mwFactory);
        var policyBuilder = new PolicySpecBuilder(mwFactory);
        var ordering = new MiddlewareOrdering();

        // Base options
        var baseOptions = optionsFactory.Create(compilation, data.Options);

        // Discover handlers
        var discovery = new RoslynHandlerDiscovery(
            Known.CoreNamespace,
            baseOptions.IncludeNamespacePrefix,
            baseOptions.CommandContextType);

        var discoveryResult = discovery.Discover(compilation, handlerSymbols);

        // Always emit contribution + module initializer (+ empty pipeline contribution)
        new ModuleInitializerEmitter().Emit(roslynContext, discoveryResult, baseOptions);
        new ContributionEmitter().Emit(roslynContext, discoveryResult, baseOptions);
        new EmptyPipelineContributionEmitter().Emit(roslynContext, discoveryResult, baseOptions);
        // always emits (empty if disabled)
        new HandlerRegistrationsEmitter().Emit(roslynContext, discoveryResult, baseOptions);

        // -----------------------------------------------------------------
        // HOST GATE:
        // If no UseTinyDispatcher calls in this project → do not emit pipelines.
        // -----------------------------------------------------------------
        if (useTinyCalls.IsDefaultOrEmpty || useTinyCalls.Length == 0)
            return;

        // -----------------------------------------------------------------
        // Infer CommandContextType from UseTinyDispatcher<TContext> (SYNTAX-based)
        // -----------------------------------------------------------------
        var inferredCtx = ctxInference.TryInferContextTypeFromUseTinyCalls(useTinyCalls, compilation);
        var effectiveOptions = optionsFactory.ApplyInferredContextIfMissing(baseOptions, inferredCtx);

        // If still no ctx, we cannot generate pipelines (PipelineEmitter requires closed ctx)
        if (string.IsNullOrWhiteSpace(effectiveOptions.CommandContextType))
            return;

        var expectedContextFqn = Fqn.EnsureGlobal(effectiveOptions.CommandContextType!);

        // -----------------------------------------------------------------
        // Middleware + Policy discovery from TinyBootstrap fluent calls
        // -----------------------------------------------------------------
        var globalEntries = new List<OrderedEntry>();
        var perCmdEntries = new List<OrderedPerCommandEntry>();
        var policyTypeSymbols = new List<INamedTypeSymbol>();
        var diags = new List<Diagnostic>();

        for (var i = 0; i < useTinyCalls.Length; i++)
        {
            extractor.Extract(
                useTinyCalls[i],
                compilation,
                expectedContextFqn,
                globalEntries,
                perCmdEntries,
                policyTypeSymbols,
                diags);
        }

        // Policies: build PolicySpec map (policyTypeFqn -> PolicySpec)
        var policies = policyBuilder.Build(compilation, expectedContextFqn, policyTypeSymbols, diags);

        if (diags.Count > 0)
        {
            for (var i = 0; i < diags.Count; i++)
                roslynContext.ReportDiagnostic(diags[i]);
            return;
        }

        // Order + distinct middleware
        var globals = ordering.OrderAndDistinctGlobals(globalEntries);
        var perCmd = ordering.BuildPerCommandMap(perCmdEntries);

        // If nothing at all, skip
        var hasAny =
            globals.Length > 0 ||
            perCmd.Count > 0 ||
            policies.Count > 0;

        if (!hasAny)
            return;

        // Emit pipelines
        new PipelineEmitter(globals, perCmd, policies)
            .Emit(roslynContext, discoveryResult, effectiveOptions);
    }
}
