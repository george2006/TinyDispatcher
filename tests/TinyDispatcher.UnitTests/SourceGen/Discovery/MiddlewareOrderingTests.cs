using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Moq;
using TinyDispatcher.SourceGen.Discovery;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.Discovery;

public sealed class MiddlewareOrderingTests
{
    [Fact]
    public void Order_and_distinct_globals_when_items_are_empty_returns_empty()
    {
        var sut = new MiddlewareOrdering();

        var result = sut.OrderAndDistinctGlobals(new List<OrderedEntry>());

        Assert.Equal(ImmutableArray<MiddlewareRef>.Empty, result);
    }

    [Fact]
    public void Order_and_distinct_globals_orders_by_file_path_then_span_start_and_removes_duplicates()
    {
        var sut = new MiddlewareOrdering();

        var middleware1 = CreateMiddlewareRef("Middleware1");
        var middleware2 = CreateMiddlewareRef("Middleware2");
        var middleware3 = CreateMiddlewareRef("Middleware3");

        var items = new List<OrderedEntry>
        {
            new(middleware2, new OrderKey("b.cs", 20)),
            new(middleware1, new OrderKey("a.cs", 30)),
            new(middleware3, new OrderKey("a.cs", 10)),
            new(middleware1, new OrderKey("z.cs", 99)), // duplicate
            new(middleware2, new OrderKey("b.cs", 40))  // duplicate
        };

        var result = sut.OrderAndDistinctGlobals(items);

        Assert.Equal(3, result.Length);
        Assert.Equal(middleware3, result[0]);
        Assert.Equal(middleware1, result[1]);
        Assert.Equal(middleware2, result[2]);
    }

    [Fact]
    public void Build_per_command_map_when_items_are_empty_returns_empty()
    {
        var sut = new MiddlewareOrdering();

        var result = sut.BuildPerCommandMap(new List<OrderedPerCommandEntry>());

        Assert.Empty(result);
    }

    [Fact]
    public void Build_per_command_map_groups_by_command_orders_and_removes_duplicates_per_command()
    {
        var sut = new MiddlewareOrdering();

        var middleware1 = CreateMiddlewareRef("Middleware1");
        var middleware2 = CreateMiddlewareRef("Middleware2");
        var middleware3 = CreateMiddlewareRef("Middleware3");

        var items = new List<OrderedPerCommandEntry>
        {
            new("CommandB", middleware2, new OrderKey("b.cs", 20)),
            new("CommandA", middleware1, new OrderKey("b.cs", 30)),
            new("CommandA", middleware3, new OrderKey("a.cs", 10)),
            new("CommandA", middleware1, new OrderKey("z.cs", 99)), // duplicate
            new("CommandB", middleware2, new OrderKey("c.cs", 50)), // duplicate
            new("CommandB", middleware1, new OrderKey("a.cs", 5))
        };

        var result = sut.BuildPerCommandMap(items);

        Assert.Equal(2, result.Count);

        Assert.Equal(2, result["CommandA"].Length);
        Assert.Equal(middleware3, result["CommandA"][0]);
        Assert.Equal(middleware1, result["CommandA"][1]);

        Assert.Equal(2, result["CommandB"].Length);
        Assert.Equal(middleware1, result["CommandB"][0]);
        Assert.Equal(middleware2, result["CommandB"][1]);
    }

    [Fact]
    public void Build_per_command_map_allows_same_middleware_for_different_commands()
    {
        var sut = new MiddlewareOrdering();

        var shared = CreateMiddlewareRef("SharedMiddleware");

        var items = new List<OrderedPerCommandEntry>
        {
            new("CommandA", shared, new OrderKey("a.cs", 10)),
            new("CommandB", shared, new OrderKey("a.cs", 20))
        };

        var result = sut.BuildPerCommandMap(items);

        Assert.Single(result["CommandA"]);
        Assert.Single(result["CommandB"]);

        Assert.Equal(shared, result["CommandA"][0]);
        Assert.Equal(shared, result["CommandB"][0]);
    }

    private static MiddlewareRef CreateMiddlewareRef(string openTypeFqn, int arity = 2)
    {
        var symbol = new Mock<INamedTypeSymbol>().Object;
        return new MiddlewareRef(symbol, openTypeFqn, arity);
    }
}