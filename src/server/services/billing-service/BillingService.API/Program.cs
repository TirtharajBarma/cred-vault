using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Shared.Contracts.Configuration;
using Shared.Contracts.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebClients", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());

builder.Services.AddDbContext<BillingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb"));
});

var cardServiceBaseUrl = builder.Configuration["Services:CardService:BaseUrl"];
if (string.IsNullOrWhiteSpace(cardServiceBaseUrl))
{
    cardServiceBaseUrl = "http://localhost:5002/";
}

builder.Services.AddHttpClient<CardServiceClient>(client =>
{
    client.BaseAddress = new Uri(cardServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Billing Service API";
    });
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
