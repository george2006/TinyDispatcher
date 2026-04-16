using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Discovery;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.Discovery;

public sealed class PolicySpecBuilderTests
{
    [Fact]
    public void Build_when_policies_are_empty_returns_empty()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}
""");

        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol>());

        Assert.Empty(result);
    }

    [Fact]
    public void Build_ignores_types_without_tiny_policy_attribute()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}

namespace Demo
{
    public class SomeCommand { }
    public class SomeMiddleware<TCommand, TContext> { }

    [TinyDispatcher.UseMiddleware(typeof(SomeMiddleware<,>))]
    [TinyDispatcher.ForCommand(typeof(SomeCommand))]
    public class NotAPolicy
    {
    }
}
""");

        var policy = GetNamedType(compilation, "Demo.NotAPolicy");
        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol> { policy });

        Assert.Empty(result);
    }

    [Fact]
    public void Build_returns_policy_with_distinct_middlewares_and_commands()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}

namespace Demo
{
    public class CommandA { }
    public class CommandB { }

    public class MiddlewareA<TCommand, TContext> { }
    public class MiddlewareB<TCommand, TContext> { }

    [TinyDispatcher.TinyPolicy]
    [TinyDispatcher.UseMiddleware(typeof(MiddlewareA<,>))]
    [TinyDispatcher.UseMiddleware(typeof(MiddlewareA<,>))]
    [TinyDispatcher.UseMiddleware(typeof(MiddlewareB<,>))]
    [TinyDispatcher.ForCommand(typeof(CommandA))]
    [TinyDispatcher.ForCommand(typeof(CommandA))]
    [TinyDispatcher.ForCommand(typeof(CommandB))]
    public class CheckoutPolicy
    {
    }
}
""");

        var policy = GetNamedType(compilation, "Demo.CheckoutPolicy");
        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol> { policy });

        var entry = Assert.Single(result);
        Assert.Equal("global::Demo.CheckoutPolicy", entry.Key);

        var spec = entry.Value;
        Assert.Equal("global::Demo.CheckoutPolicy", spec.PolicyTypeFqn);

        Assert.Equal(2, spec.Middlewares.Length);
        Assert.Equal("global::Demo.MiddlewareA", spec.Middlewares[0].OpenTypeFqn);
        Assert.Equal("global::Demo.MiddlewareB", spec.Middlewares[1].OpenTypeFqn);
        Assert.All(spec.Middlewares, x => Assert.Equal(2, x.Arity));

        Assert.Equal(2, spec.Commands.Length);
        Assert.Equal("global::Demo.CommandA", spec.Commands[0]);
        Assert.Equal("global::Demo.CommandB", spec.Commands[1]);
    }

    [Fact]
    public void Build_ignores_policy_without_middlewares()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}

namespace Demo
{
    public class CommandA { }

    [TinyDispatcher.TinyPolicy]
    [TinyDispatcher.ForCommand(typeof(CommandA))]
    public class PolicyWithoutMiddleware
    {
    }
}
""");

        var policy = GetNamedType(compilation, "Demo.PolicyWithoutMiddleware");
        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol> { policy });

        Assert.Empty(result);
    }

    [Fact]
    public void Build_ignores_policy_without_commands()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}

namespace Demo
{
    public class MiddlewareA<TCommand, TContext> { }

    [TinyDispatcher.TinyPolicy]
    [TinyDispatcher.UseMiddleware(typeof(MiddlewareA<,>))]
    public class PolicyWithoutCommands
    {
    }
}
""");

        var policy = GetNamedType(compilation, "Demo.PolicyWithoutCommands");
        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol> { policy });

        Assert.Empty(result);
    }

    [Fact]
    public void Build_distincts_duplicate_policy_symbols()
    {
        var compilation = CreateCompilation("""
using System;

namespace TinyDispatcher
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class TinyPolicyAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class UseMiddlewareAttribute : Attribute
    {
        public UseMiddlewareAttribute(Type type) { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class ForCommandAttribute : Attribute
    {
        public ForCommandAttribute(Type type) { }
    }
}

namespace Demo
{
    public class CommandA { }
    public class MiddlewareA<TCommand, TContext> { }

    [TinyDispatcher.TinyPolicy]
    [TinyDispatcher.UseMiddleware(typeof(MiddlewareA<,>))]
    [TinyDispatcher.ForCommand(typeof(CommandA))]
    public class CheckoutPolicy
    {
    }
}
""");

        var policy = GetNamedType(compilation, "Demo.CheckoutPolicy");
        var sut = new PolicySpecBuilder();

        var result = sut.Build(new List<INamedTypeSymbol> { policy, policy });

        var entry = Assert.Single(result);
        Assert.Equal("global::Demo.CheckoutPolicy", entry.Key);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { syntaxTree },
            references: new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location)
            },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static INamedTypeSymbol GetNamedType(Compilation compilation, string metadataName)
    {
        return compilation.GetTypeByMetadataName(metadataName)
               ?? throw new Xunit.Sdk.XunitException($"Type '{metadataName}' was not found.");
    }
}
