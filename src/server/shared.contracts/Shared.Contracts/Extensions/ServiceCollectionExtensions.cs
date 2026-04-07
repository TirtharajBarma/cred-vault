using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Swashbuckle.AspNetCore.Swagger;
using Shared.Contracts.Configuration;
using MassTransit;

namespace Shared.Contracts.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStandardAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        
        var jwtOptions = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey));

        services
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

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddStandardApi(this IServiceCollection services)
    {
        services.AddOpenApi();
        services.AddSwaggerGen();
        services.AddProblemDetails();
        return services;
    }

    public static WebApplication UseStandardApi(this WebApplication app, string title)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", title);
            });
        }
        return app;
    }

    public static IServiceCollection AddStandardMessaging(
        this IServiceCollection services, 
        IConfiguration configuration, 
        Action<IBusRegistrationConfigurator>? configure = null,
        string? serviceName = null)
    {
        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            
            configure?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
                {
                    h.Username(configuration["RabbitMQ:Username"] ?? "guest");
                    h.Password(configuration["RabbitMQ:Password"] ?? "guest");
                });

                // Auto-configure endpoints with service name prefix if provided
                if (!string.IsNullOrEmpty(serviceName))
                {
                    cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter(serviceName + "-", false));
                }
                else
                {
                    cfg.ConfigureEndpoints(context);
                }
            });
        });

        return services;
    }
}
