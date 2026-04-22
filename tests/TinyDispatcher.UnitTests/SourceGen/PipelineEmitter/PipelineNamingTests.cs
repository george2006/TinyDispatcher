#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelineNamingTests
{
    private static MiddlewareRef Mw(string openTypeFqn, int arity)
        => new MiddlewareRef(OpenTypeFqn: openTypeFqn, Arity: arity);

    [Fact]
    public void Normalize_fqn_adds_global_prefix_when_missing()
    {
        var result = PipelineTypeNames.NormalizeFqn("MyApp.Foo");

        Assert.Equal("global::MyApp.Foo", result);
    }

    [Fact]
    public void Normalize_fqn_deduplicates_double_global_prefix()
    {
        var result = PipelineTypeNames.NormalizeFqn("global::global::MyApp.Foo");

        Assert.Equal("global::MyApp.Foo", result);
    }

    [Fact]
    public void Close_middleware_with_arity_2_closes_command_and_context()
    {
        var mw = Mw("global::MyApp.Mw", 2);

        var closed = PipelineTypeNames.CloseMiddleware(mw, "TCommand", "global::MyApp.Ctx");

        Assert.Equal("global::MyApp.Mw<TCommand, global::MyApp.Ctx>", closed);
    }

    [Fact]
    public void Close_middleware_with_arity_1_closes_command_only()
    {
        var mw = Mw("global::MyApp.Mw", 1);

        var closed = PipelineTypeNames.CloseMiddleware(mw, "TCommand", "global::MyApp.Ctx");

        Assert.Equal("global::MyApp.Mw<TCommand>", closed);
    }

    [Fact]
    public void Open_generic_typeof_with_arity_2_is_two_parameter_open_generic()
    {
        var mw = Mw("global::MyApp.Mw", 2);

        var open = PipelineTypeNames.OpenGenericTypeof(mw);

        Assert.Equal("global::MyApp.Mw<,>", open);
    }

    [Fact]
    public void Open_generic_typeof_with_arity_1_is_one_parameter_open_generic()
    {
        var mw = Mw("global::MyApp.Mw", 1);

        var open = PipelineTypeNames.OpenGenericTypeof(mw);

        Assert.Equal("global::MyApp.Mw<>", open);
    }

    [Fact]
    public void Normalize_distinct_removes_duplicates_by_open_type_and_arity_and_sorts()
    {
        var items = ImmutableArray.Create(
            Mw("global::B.Mw", 2),
            Mw("global::A.Mw", 2),
            Mw("global::A.Mw", 2), // dup
            Mw("global::A.Mw", 1)  // not dup (different arity)
        );

        var distinct = PipelineMiddlewareSets.NormalizeDistinct(items);

        Assert.Equal(3, distinct.Length);

        // Sorted by OpenTypeFqn
        Assert.Equal("global::A.Mw", distinct[0].OpenTypeFqn);
        Assert.Equal(2, distinct[0].Arity);

        Assert.Equal("global::A.Mw", distinct[1].OpenTypeFqn);
        Assert.Equal(1, distinct[1].Arity);

        Assert.Equal("global::B.Mw", distinct[2].OpenTypeFqn);
        Assert.Equal(2, distinct[2].Arity);
    }

    [Fact]
    public void Ctor_param_name_strips_namespace_ticks_and_middleware_suffix_and_lowercases_first_letter()
    {
        var mw = Mw("global::MyApp.Logging.GlobalLogMiddleware`2", 2);

        var name = PipelineNameFactory.CtorParamName(mw);

        Assert.Equal("globalLog", name);
    }

    [Fact]
    public void Ctor_param_name_when_type_name_is_single_letter_lowercases_only()
    {
        var mw = Mw("global::MyApp.XMiddleware", 1);

        var name = PipelineNameFactory.CtorParamName(mw);

        Assert.Equal("x", name);
    }

    [Fact]
    public void Field_name_is_ctor_param_name_prefixed_with_underscore()
    {
        var mw = Mw("global::MyApp.GlobalLogMiddleware", 2);

        var field = PipelineNameFactory.FieldName(mw);

        Assert.Equal("_globalLog", field);
    }

    [Fact]
    public void Sanitize_command_name_replaces_invalid_chars_with_underscore()
    {
        var cmd = "global::MyApp.Commands.Do-Thing+Inner";

        var name = PipelineNameFactory.SanitizeCommandName(cmd);

        Assert.Equal("Do_Thing_Inner", name);
    }

    [Fact]
    public void Sanitize_policy_name_replaces_invalid_chars_with_underscore()
    {
        var policy = "global::MyApp.Policies.CheckoutPolicy+Nested";

        var name = PipelineNameFactory.SanitizePolicyName(policy);

        Assert.Equal("MyApp_Policies_CheckoutPolicy_Nested", name);
    }
}

