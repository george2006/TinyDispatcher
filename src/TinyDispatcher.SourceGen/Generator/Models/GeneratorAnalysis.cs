using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using TinyDispatcher.SourceGen.Validation;

namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record GeneratorAnalysis(
    Compilation Compilation,
    ImmutableArray<InvocationExpressionSyntax> UseTinyCallsSyntax,
    DiscoveryResult Discovery,
    GeneratorOptions EffectiveOptions,
    GeneratorValidationContext ValidationContext,
    ImmutableArray<MiddlewareRef> Globals,
    ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand,
    ImmutableDictionary<string, PolicySpec> Policies);
