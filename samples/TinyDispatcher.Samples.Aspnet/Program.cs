// Program.cs
#nullable enable

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Minimal API baseline: no controllers, no MVC
var app = builder.Build();

app.MapGet("/", () => Results.Ok(new { name = "TinyDispatcher ASP.NET Sample", status = "ok" }));

app.Run();
