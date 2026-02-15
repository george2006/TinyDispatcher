#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelineEmitterHelpersTests
{
    [Fact]
    public void Normalize_fqn_adds_global_prefix_when_missing()
    {
        var result = TypeNames.NormalizeFqn("MyApp.Foo");

        Assert.Equal("global::MyApp.Foo", result);
    }

    [Fact]
    public void Normalize_fqn_deduplicates_double_global_prefix()
    {
        var result = TypeNames.NormalizeFqn("global::global::MyApp.Foo");

        Assert.Equal("global::MyApp.Foo", result);
    }

    [Fact]
    public void Close_middleware_with_arity_2_closes_command_and_context()
    {
        var mw = new MiddlewareRef("global::MyApp.Mw", 2);

        var closed = TypeNames.CloseMiddleware(mw, "TCommand", "global::MyApp.Ctx");

        Assert.Equal("global::MyApp.Mw<TCommand, global::MyApp.Ctx>", closed);
    }

    [Fact]
    public void Close_middleware_with_arity_1_closes_command_only()
    {
        var mw = new MiddlewareRef("global::MyApp.Mw", 1);

        var closed = TypeNames.CloseMiddleware(mw, "TCommand", "global::MyApp.Ctx");

        Assert.Equal("global::MyApp.Mw<TCommand>", closed);
    }

    [Fact]
    public void Open_generic_typeof_with_arity_2_is_two_parameter_open_generic()
    {
        var mw = new MiddlewareRef("global::MyApp.Mw", 2);

        var open = TypeNames.OpenGenericTypeof(mw);

        Assert.Equal("global::MyApp.Mw<,>", open);
    }

    [Fact]
    public void Open_generic_typeof_with_arity_1_is_one_parameter_open_generic()
    {
        var mw = new MiddlewareRef("global::MyApp.Mw", 1);

        var open = TypeNames.OpenGenericTypeof(mw);

        Assert.Equal("global::MyApp.Mw<>", open);
    }

    [Fact]
    public void Normalize_distinct_removes_duplicates_by_open_type_and_arity_and_sorts()
    {
        var items = ImmutableArray.Create(
            new MiddlewareRef("global::B.Mw", 2),
            new MiddlewareRef("global::A.Mw", 2),
            new MiddlewareRef("global::A.Mw", 2), // dup
            new MiddlewareRef("global::A.Mw", 1)  // not dup (different arity)
        );

        var distinct = MiddlewareSets.NormalizeDistinct(items);

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
        var mw = new MiddlewareRef("global::MyApp.Logging.GlobalLogMiddleware`2", 2);

        var name = NameFactory.CtorParamName(mw);

        Assert.Equal("globalLog", name);
    }

    [Fact]
    public void Ctor_param_name_when_type_name_is_single_letter_lowercases_only()
    {
        var mw = new MiddlewareRef("global::MyApp.XMiddleware", 1);

        var name = NameFactory.CtorParamName(mw);

        Assert.Equal("x", name);
    }

    [Fact]
    public void Field_name_is_ctor_param_name_prefixed_with_underscore()
    {
        var mw = new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2);

        var field = NameFactory.FieldName(mw);

        Assert.Equal("_globalLog", field);
    }

    [Fact]
    public void Sanitize_command_name_replaces_invalid_chars_with_underscore()
    {
        var cmd = "global::MyApp.Commands.Do-Thing+Inner";

        var name = NameFactory.SanitizeCommandName(cmd);

        Assert.Equal("Do_Thing_Inner", name);
    }

    [Fact]
    public void Sanitize_policy_name_replaces_invalid_chars_with_underscore()
    {
        var policy = "global::MyApp.Policies.CheckoutPolicy+Nested";

        var name = NameFactory.SanitizePolicyName(policy);

        Assert.Equal("MyApp_Policies_CheckoutPolicy_Nested", name);
    }
}
