using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Shared.Contracts.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddStandardCors();

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddOpenApi();
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "gateway" }));

app.UseCors("AllowWebClients");
await app.UseOcelot();

app.Run();
