#nullable enable

using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters;

public sealed class PipelineEmitterRefactored : ICodeEmitter
{
    private readonly ImmutableArray<MiddlewareRef> _globalMiddlewares;
    private readonly ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> _perCommand;
    private readonly ImmutableDictionary<string, PolicySpec> _policies;

    public PipelineEmitterRefactored(
        ImmutableArray<MiddlewareRef> globalMiddlewares,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        _globalMiddlewares = globalMiddlewares;
        _perCommand = perCommand ?? ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;
        _policies = policies ?? ImmutableDictionary<string, PolicySpec>.Empty;
    }

    public void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options)
    {
        if (options is null) return;
        if (string.IsNullOrWhiteSpace(options.CommandContextType)) return;

        var hasAny =
            (!_globalMiddlewares.IsDefaultOrEmpty && _globalMiddlewares.Length > 0) ||
            (_perCommand.Count > 0) ||
            (_policies.Count > 0);

        if (!hasAny) return;

        var plan = PipelinePlanner.Build(_globalMiddlewares, _perCommand, _policies, result, options);
        if (!plan.ShouldEmit) return;

        var source = PipelineSourceWriter.Write(plan);

        context.AddSource(
            hintName: "TinyDispatcherPipeline.g.cs",
            sourceText: SourceText.From(source, Encoding.UTF8));
    }

    // ============================================================
    // CodeWriter (brace-safe emission)
    // ============================================================

    internal sealed class CodeWriter
    {
        private readonly StringBuilder _sb;
        private int _indent;
        private readonly Stack<string> _blocks = new();

        public CodeWriter(int capacity = 96_000) => _sb = new StringBuilder(capacity);

        public override string ToString() => _sb.ToString();

        public void Line(string text = "")
        {
            if (text.Length == 0)
            {
                _sb.AppendLine();
                return;
            }

            _sb.Append(' ', _indent * 2);
            _sb.AppendLine(text);
        }

        public void BeginBlock(string headerLine)
        {
            Line(headerLine);
            Line("{");
            _blocks.Push(headerLine);
            _indent++;
        }

        public void BeginAnonymousBlock(string labelForDebug = "{")
        {
            Line("{");
            _blocks.Push(labelForDebug);
            _indent++;
        }

        public void EndBlock()
        {
            if (_blocks.Count == 0)
                throw new InvalidOperationException("Attempted to close a block but none are open.");

            _indent--;
            Line("}");
            _blocks.Pop();
        }

        public void EnsureAllBlocksClosed()
        {
            if (_blocks.Count != 0)
                throw new InvalidOperationException("Unclosed block(s). Top block: " + _blocks.Peek());
        }
    }

    // ============================================================
    // PLAN
    // ============================================================

    internal sealed record PipelinePlan(
        string GeneratedNamespace,
        string ContextFqn,
        string CoreFqn,
        bool ShouldEmit,
        PipelineDefinition? GlobalPipeline,
        ImmutableArray<PipelineDefinition> PolicyPipelines,
        ImmutableArray<PipelineDefinition> PerCommandPipelines,
        ImmutableArray<OpenGenericRegistration> OpenGenericMiddlewareRegistrations,
        ImmutableArray<ServiceRegistration> ServiceRegistrations
    );

    internal sealed record PipelineDefinition(
        string ClassName,
        bool IsOpenGeneric,
        string CommandType, // "TCommand" or FQN
        ImmutableArray<MiddlewareStep> Steps
    );

    internal sealed record MiddlewareStep(MiddlewareRef Middleware);

    internal sealed record OpenGenericRegistration(string TypeofExpression);

    internal sealed record ServiceRegistration(string ServiceTypeExpression, string ImplementationTypeExpression);

    internal static class PipelinePlanner
    {
        public static PipelinePlan Build(
            ImmutableArray<MiddlewareRef> globalMiddlewares,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
            ImmutableDictionary<string, PolicySpec> policies,
            DiscoveryResult discovery,
            GeneratorOptions options)
        {
            var core = "global::TinyDispatcher";
            var genNs = options.GeneratedNamespace;
            var ctx = TypeNames.NormalizeFqn(options.CommandContextType!);

            var global = MiddlewareSets.NormalizeDistinct(globalMiddlewares);
            var hasGlobal = global.Length > 0;

            var perCmd = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);
            foreach (var kv in perCommand)
            {
                var cmd = TypeNames.NormalizeFqn(kv.Key);
                var mids = MiddlewareSets.NormalizeDistinct(kv.Value);
                if (string.IsNullOrWhiteSpace(cmd) || mids.Length == 0) continue;
                perCmd[cmd] = mids;
            }

            var cmdToPolicyMids = BuildCommandToPolicyMiddlewares(policies);

            PipelineDefinition? globalPipeline = null;
            if (hasGlobal)
            {
                globalPipeline = new PipelineDefinition(
                    ClassName: "TinyDispatcherGlobalPipeline",
                    IsOpenGeneric: true,
                    CommandType: "TCommand",
                    Steps: global.Select(m => new MiddlewareStep(m)).ToImmutableArray());
            }

            var policyPipelines = BuildPolicyPipelines(global, policies);
            var perCommandPipelines = BuildPerCommandPipelines(global, perCmd, cmdToPolicyMids);
            var mwRegs = BuildOpenGenericMiddlewareRegistrations(global, perCommand, policies);
            var svcRegs = BuildServiceRegistrations(genNs, core, ctx, hasGlobal, discovery, perCmd, policies);

            var shouldEmit =
                globalPipeline is not null ||
                policyPipelines.Length > 0 ||
                perCommandPipelines.Length > 0 ||
                mwRegs.Length > 0 ||
                svcRegs.Length > 0;

            return new PipelinePlan(
                GeneratedNamespace: genNs,
                ContextFqn: ctx,
                CoreFqn: core,
                ShouldEmit: shouldEmit,
                GlobalPipeline: globalPipeline,
                PolicyPipelines: policyPipelines,
                PerCommandPipelines: perCommandPipelines,
                OpenGenericMiddlewareRegistrations: mwRegs,
                ServiceRegistrations: svcRegs);
        }

        private static ImmutableArray<PipelineDefinition> BuildPolicyPipelines(
            MiddlewareRef[] global,
            ImmutableDictionary<string, PolicySpec> policies)
        {
            if (policies.Count == 0) return ImmutableArray<PipelineDefinition>.Empty;

            var list = new List<PipelineDefinition>(policies.Count);

            foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
            {
                var policyMids = MiddlewareSets.NormalizeDistinct(p.Middlewares);
                if (policyMids.Length == 0) continue;

                // ORDER: Global -> Policy -> Handler
                var steps = new List<MiddlewareStep>(global.Length + policyMids.Length);
                for (int i = 0; i < global.Length; i++) steps.Add(new MiddlewareStep(global[i]));
                for (int i = 0; i < policyMids.Length; i++) steps.Add(new MiddlewareStep(policyMids[i]));

                list.Add(new PipelineDefinition(
                    ClassName: "TinyDispatcherPolicyPipeline_" + NameFactory.SanitizePolicyName(p.PolicyTypeFqn),
                    IsOpenGeneric: true,
                    CommandType: "TCommand",
                    Steps: steps.ToImmutableArray()));
            }

            return list.ToImmutableArray();
        }

        private static ImmutableArray<PipelineDefinition> BuildPerCommandPipelines(
            MiddlewareRef[] global,
            Dictionary<string, MiddlewareRef[]> perCmd,
            Dictionary<string, MiddlewareRef[]> cmdToPolicyMids)
        {
            if (perCmd.Count == 0) return ImmutableArray<PipelineDefinition>.Empty;

            var list = new List<PipelineDefinition>(perCmd.Count);

            foreach (var kv in perCmd.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var cmdFqn = kv.Key;
                var perCmdMids = kv.Value;

                if (!cmdToPolicyMids.TryGetValue(cmdFqn, out var policyMids))
                    policyMids = Array.Empty<MiddlewareRef>();

                // ORDER: Global -> Policy -> PerCommand -> Handler
                var steps = new List<MiddlewareStep>(global.Length + policyMids.Length + perCmdMids.Length);
                for (int i = 0; i < global.Length; i++) steps.Add(new MiddlewareStep(global[i]));
                for (int i = 0; i < policyMids.Length; i++) steps.Add(new MiddlewareStep(policyMids[i]));
                for (int i = 0; i < perCmdMids.Length; i++) steps.Add(new MiddlewareStep(perCmdMids[i]));

                list.Add(new PipelineDefinition(
                    ClassName: "TinyDispatcherPipeline_" + NameFactory.SanitizeCommandName(cmdFqn),
                    IsOpenGeneric: false,
                    CommandType: cmdFqn,
                    Steps: steps.ToImmutableArray()));
            }

            return list.ToImmutableArray();
        }

        private static Dictionary<string, MiddlewareRef[]> BuildCommandToPolicyMiddlewares(
            ImmutableDictionary<string, PolicySpec> policies)
        {
            var map = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

            foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
            {
                var mids = MiddlewareSets.NormalizeDistinct(p.Middlewares);
                if (mids.Length == 0) continue;

                for (int i = 0; i < p.Commands.Length; i++)
                {
                    var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    if (!map.ContainsKey(cmd))
                        map[cmd] = mids; // first wins
                }
            }

            return map;
        }

        private static ImmutableArray<OpenGenericRegistration> BuildOpenGenericMiddlewareRegistrations(
            MiddlewareRef[] global,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
            ImmutableDictionary<string, PolicySpec> policies)
        {
            var all = new List<MiddlewareRef>(256);

            all.AddRange(global);

            foreach (var kv in perCommand)
                all.AddRange(MiddlewareSets.NormalizeDistinct(kv.Value));

            foreach (var p in policies.Values)
                all.AddRange(MiddlewareSets.NormalizeDistinct(p.Middlewares));

            var distinct = MiddlewareSets.NormalizeDistinct(all.ToImmutableArray());

            var regs = new List<OpenGenericRegistration>(distinct.Length);
            for (int i = 0; i < distinct.Length; i++)
                regs.Add(new OpenGenericRegistration(TypeNames.OpenGenericTypeof(distinct[i])));

            return regs.ToImmutableArray();
        }

        private static ImmutableArray<ServiceRegistration> BuildServiceRegistrations(
            string genNs,
            string core,
            string ctx,
            bool hasGlobal,
            DiscoveryResult discovery,
            Dictionary<string, MiddlewareRef[]> perCmd,
            ImmutableDictionary<string, PolicySpec> policies)
        {
            var perCmdSet = new HashSet<string>(perCmd.Keys, StringComparer.Ordinal);

            var cmdToPolicyPipelineOpen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
            {
                var open = "global::" + genNs + ".TinyDispatcherPolicyPipeline_" + NameFactory.SanitizePolicyName(p.PolicyTypeFqn);

                for (int i = 0; i < p.Commands.Length; i++)
                {
                    var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    if (!cmdToPolicyPipelineOpen.ContainsKey(cmd))
                        cmdToPolicyPipelineOpen[cmd] = open;
                }
            }

            var policyCmdSet = new HashSet<string>(cmdToPolicyPipelineOpen.Keys, StringComparer.Ordinal);

            var regs = new List<ServiceRegistration>(256);

            foreach (var cmd in perCmdSet.OrderBy(x => x, StringComparer.Ordinal))
            {
                regs.Add(new ServiceRegistration(
                    ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                    ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherPipeline_{NameFactory.SanitizeCommandName(cmd)}"));
            }

            foreach (var cmd in policyCmdSet.OrderBy(x => x, StringComparer.Ordinal))
            {
                if (perCmdSet.Contains(cmd)) continue;

                regs.Add(new ServiceRegistration(
                    ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                    ImplementationTypeExpression: $"{cmdToPolicyPipelineOpen[cmd]}<{cmd}>"));
            }

            if (hasGlobal && discovery != null && discovery.Commands.Length > 0)
            {
                for (int i = 0; i < discovery.Commands.Length; i++)
                {
                    var cmd = TypeNames.NormalizeFqn(discovery.Commands[i].MessageTypeFqn);
                    if (string.IsNullOrWhiteSpace(cmd)) continue;

                    if (perCmdSet.Contains(cmd)) continue;
                    if (policyCmdSet.Contains(cmd)) continue;

                    regs.Add(new ServiceRegistration(
                        ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                        ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherGlobalPipeline<{cmd}>"));
                }
            }

            return regs.ToImmutableArray();
        }
    }

    // ============================================================
    // WRITE (brace-safe emission)
    // ============================================================

    internal static class PipelineSourceWriter
    {
        public static string Write(PipelinePlan plan)
        {
            var w = new CodeWriter();

            w.Line("// <auto-generated/>");
            w.Line("#nullable enable");
            w.Line("using System;");
            w.Line("using System.Threading;");
            w.Line("using System.Threading.Tasks;");
            w.Line("using Microsoft.Extensions.DependencyInjection;");
            w.Line("using Microsoft.Extensions.DependencyInjection.Extensions;");
            w.Line();

            w.BeginBlock($"namespace {plan.GeneratedNamespace}");

            if (plan.GlobalPipeline is not null)
                WritePipeline(w, plan, plan.GlobalPipeline);

            for (int i = 0; i < plan.PolicyPipelines.Length; i++)
                WritePipeline(w, plan, plan.PolicyPipelines[i]);

            for (int i = 0; i < plan.PerCommandPipelines.Length; i++)
                WritePipeline(w, plan, plan.PerCommandPipelines[i]);

            w.EndBlock(); // namespace

            w.Line();

            w.BeginBlock($"namespace {plan.GeneratedNamespace}");
            WriteContribution(w, plan);
            w.EndBlock(); // namespace

            w.EnsureAllBlocksClosed();
            return w.ToString();
        }

        private static void WritePipeline(CodeWriter w, PipelinePlan plan, PipelineDefinition def)
        {
            var core = plan.CoreFqn;
            var ctx = plan.ContextFqn;
            var cmdType = def.IsOpenGeneric ? "TCommand" : def.CommandType;

            if (def.IsOpenGeneric)
            {
                // IMPORTANT: do NOT use BeginBlock for "where". Emit where line, then open the class body as an anonymous block.
                w.Line($"internal sealed class {def.ClassName}<TCommand> : {core}.ICommandPipeline<TCommand, {ctx}>");
                w.Line($"  where TCommand : {core}.ICommand");
                w.BeginAnonymousBlock($"class {def.ClassName}<TCommand>");
            }
            else
            {
                w.BeginBlock($"internal sealed class {def.ClassName} : {core}.ICommandPipeline<{def.CommandType}, {ctx}>");
            }

            // Fields (distinct by open type + arity)
            var mwDistinct = MiddlewareSets
                .DistinctByOpenTypeAndArity(def.Steps.Select(s => s.Middleware))
                .ToArray();

            for (int i = 0; i < mwDistinct.Length; i++)
            {
                var mw = mwDistinct[i];
                w.Line($"private readonly {TypeNames.CloseMiddleware(mw, cmdType, ctx)} {NameFactory.FieldName(mw)};");
            }

            w.Line("private int _index;");

            if (def.IsOpenGeneric)
                w.Line($"private {core}.ICommandHandler<TCommand, {ctx}>? _handler;");
            else
                w.Line($"private {core}.ICommandHandler<{def.CommandType}, {ctx}>? _handler;");

            w.Line("private readonly Runtime _runtime;");
            w.Line();

            WriteCtor(w, plan, def, cmdType, mwDistinct);
            WriteExecute(w, plan, def, cmdType);
            WriteNext(w, plan, def, cmdType);
            WriteRuntime(w, plan, def, cmdType);

            w.EndBlock(); // class body
            w.Line();
        }

        private static void WriteCtor(CodeWriter w, PipelinePlan plan, PipelineDefinition def, string cmdType, MiddlewareRef[] mwDistinct)
        {
            var ctx = plan.ContextFqn;

            // ctor signature (NON-generic name!)
            w.Line($"public {def.ClassName}(");
            for (int i = 0; i < mwDistinct.Length; i++)
            {
                var mw = mwDistinct[i];
                var comma = (i == mwDistinct.Length - 1) ? "" : ",";
                w.Line($"  {TypeNames.CloseMiddleware(mw, cmdType, ctx)} {NameFactory.CtorParamName(mw)}{comma}");
            }
            w.Line(")");
            w.BeginAnonymousBlock($"ctor {def.ClassName}");

            for (int i = 0; i < mwDistinct.Length; i++)
            {
                var mw = mwDistinct[i];
                w.Line($"{NameFactory.FieldName(mw)} = {NameFactory.CtorParamName(mw)};");
            }

            w.Line("_runtime = new Runtime(this);");
            w.EndBlock();

            w.Line();
        }

        private static void WriteExecute(CodeWriter w, PipelinePlan plan, PipelineDefinition def, string cmdType)
        {
            var core = plan.CoreFqn;
            var ctx = plan.ContextFqn;

            var handlerType = def.IsOpenGeneric
                ? $"{core}.ICommandHandler<TCommand, {ctx}>"
                : $"{core}.ICommandHandler<{def.CommandType}, {ctx}>";

            var cmdSig = def.IsOpenGeneric ? "TCommand" : def.CommandType;

            w.BeginBlock($"public ValueTask ExecuteAsync({cmdSig} command, {ctx} ctxValue, {handlerType} handler, CancellationToken ct = default)");
            w.Line("if (command is null) throw new ArgumentNullException(nameof(command));");
            w.Line("if (handler is null) throw new ArgumentNullException(nameof(handler));");
            w.Line("_handler = handler;");
            w.Line("_index = 0;");
            w.Line("return NextAsync(command, ctxValue, ct);");
            w.EndBlock();
            w.Line();
        }

        private static void WriteNext(CodeWriter w, PipelinePlan plan, PipelineDefinition def, string cmdType)
        {
            var ctx = plan.ContextFqn;

            w.BeginBlock($"private ValueTask NextAsync({cmdType} command, {ctx} ctxValue, CancellationToken ct)");
            w.BeginBlock("switch (_index++)");

            for (int i = 0; i < def.Steps.Length; i++)
            {
                var mw = def.Steps[i].Middleware;
                w.Line($"case {i}: return {NameFactory.FieldName(mw)}.InvokeAsync(command, ctxValue, _runtime, ct);");
            }

            w.Line("default: return new ValueTask(_handler!.HandleAsync(command, ctxValue, ct));");
            w.EndBlock(); // switch
            w.EndBlock(); // method
            w.Line();
        }

        private static void WriteRuntime(CodeWriter w, PipelinePlan plan, PipelineDefinition def, string cmdType)
        {
            var core = plan.CoreFqn;
            var ctx = plan.ContextFqn;

            w.BeginBlock($"private sealed class Runtime : {core}.Pipeline.ICommandPipelineRuntime<{cmdType}, {ctx}>");

            var pipelineType = def.IsOpenGeneric ? $"{def.ClassName}<TCommand>" : def.ClassName;

            w.Line($"private readonly {pipelineType} _p;");
            w.Line($"public Runtime({pipelineType} p) {{ _p = p; }}");

            w.BeginBlock($"public ValueTask NextAsync({cmdType} command, {ctx} ctxValue, CancellationToken ct = default)");
            w.Line("return _p.NextAsync(command, ctxValue, ct);");
            w.EndBlock();

            w.EndBlock(); // Runtime
        }

        private static void WriteContribution(CodeWriter w, PipelinePlan plan)
        {
            w.BeginBlock("internal static partial class ThisAssemblyPipelineContribution");
            w.BeginBlock("static partial void AddGeneratedPipelines(IServiceCollection services)");
            w.Line("if (services is null) throw new ArgumentNullException(nameof(services));");
            w.Line();

            if (plan.OpenGenericMiddlewareRegistrations.Length > 0)
            {
                w.Line("// Middleware open-generic registrations (required for generated pipelines ctor injection)");
                for (int i = 0; i < plan.OpenGenericMiddlewareRegistrations.Length; i++)
                    w.Line($"services.TryAddTransient(typeof({plan.OpenGenericMiddlewareRegistrations[i].TypeofExpression}));");
                w.Line();
            }

            for (int i = 0; i < plan.ServiceRegistrations.Length; i++)
            {
                var r = plan.ServiceRegistrations[i];
                w.Line($"services.AddScoped<{r.ServiceTypeExpression}, {r.ImplementationTypeExpression}>();");
            }

            w.EndBlock(); // method
            w.EndBlock(); // class
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    internal static class MiddlewareSets
    {
        public static MiddlewareRef[] NormalizeDistinct(ImmutableArray<MiddlewareRef> items)
        {
            if (items.IsDefaultOrEmpty) return Array.Empty<MiddlewareRef>();

            var list = new List<MiddlewareRef>(items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                var x = items[i];
                if (x == null) continue;

                var fqn = x.OpenTypeFqn;
                if (string.IsNullOrWhiteSpace(fqn)) continue;

                list.Add(new MiddlewareRef(TypeNames.NormalizeFqn(fqn), x.Arity));
            }

            return DistinctByOpenTypeAndArity(list).ToArray();
        }

        public static IEnumerable<MiddlewareRef> DistinctByOpenTypeAndArity(IEnumerable<MiddlewareRef> items)
        {
            return items
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.OpenTypeFqn))
                .GroupBy(m => m.OpenTypeFqn + "|" + m.Arity.ToString(), StringComparer.Ordinal)
                .Select(g => g.First())
                .OrderBy(m => m.OpenTypeFqn, StringComparer.Ordinal);
        }
    }

    internal static class TypeNames
    {
        public static string NormalizeFqn(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return string.Empty;

            var trimmed = typeName.Trim();

            if (!trimmed.StartsWith("global::", StringComparison.Ordinal))
                trimmed = "global::" + trimmed;

            if (trimmed.StartsWith("global::global::", StringComparison.Ordinal))
                trimmed = "global::" + trimmed.Substring("global::global::".Length);

            return trimmed;
        }

        public static string CloseMiddleware(MiddlewareRef mw, string cmd, string ctx)
        {
            return mw.Arity == 2
                ? mw.OpenTypeFqn + "<" + cmd + ", " + ctx + ">"
                : mw.OpenTypeFqn + "<" + cmd + ">";
        }

        public static string OpenGenericTypeof(MiddlewareRef mw)
        {
            return mw.Arity == 2
                ? mw.OpenTypeFqn + "<,>"
                : mw.OpenTypeFqn + "<>";
        }
    }

    internal static class NameFactory
    {
        public static string FieldName(MiddlewareRef mw) => "_" + CtorParamName(mw);

        public static string CtorParamName(MiddlewareRef mw)
        {
            var open = mw.OpenTypeFqn ?? string.Empty;

            var lastDot = open.LastIndexOf('.');
            var shortName = lastDot >= 0 ? open.Substring(lastDot + 1) : open;

            var tick = shortName.IndexOf('`');
            if (tick >= 0) shortName = shortName.Substring(0, tick);

            if (shortName.EndsWith("Middleware", StringComparison.Ordinal))
                shortName = shortName.Substring(0, shortName.Length - "Middleware".Length);

            if (string.IsNullOrWhiteSpace(shortName))
                shortName = "Middleware";

            return shortName.Length == 1
                ? char.ToLowerInvariant(shortName[0]).ToString()
                : char.ToLowerInvariant(shortName[0]) + shortName.Substring(1);
        }

        public static string SanitizeCommandName(string cmdFqn)
        {
            var s = cmdFqn.StartsWith("global::", StringComparison.Ordinal)
                ? cmdFqn.Substring("global::".Length)
                : cmdFqn;

            var lastDot = s.LastIndexOf('.');
            var name = lastDot >= 0 ? s.Substring(lastDot + 1) : s;

            return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }

        public static string SanitizePolicyName(string policyTypeFqn)
        {
            var s = policyTypeFqn.StartsWith("global::", StringComparison.Ordinal)
                ? policyTypeFqn.Substring("global::".Length)
                : policyTypeFqn;

            return new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        }
    }
}
