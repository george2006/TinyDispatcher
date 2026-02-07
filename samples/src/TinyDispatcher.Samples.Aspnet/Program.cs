// Program.cs
#nullable enable

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Samples.Aspnet;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTinySample();

// Minimal API baseline: no controllers, no MVC
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "TinyDispatcher ASP.NET Sample", status = "ok" }));

app.MapPost("/ping", async (
    PingRequest req,
    IDispatcher<TinyDispatcher.AppContext> dispatcher,
    CancellationToken ct) =>
{
    var message = string.IsNullOrWhiteSpace(req.Message)
        ? "hello from aspnet minimal api sample"
        : req.Message.Trim();

    await dispatcher.DispatchAsync(new Ping(message), ct);

    // ICommand dispatch returns Task (no result) → we return 200 OK
    return Results.Ok(new { ok = true });
});

app.Run();
