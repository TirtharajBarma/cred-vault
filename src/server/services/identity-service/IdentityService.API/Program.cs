using IdentityService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Auth;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()        // add extra info(like req id, correlation id)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/identity-service-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Identity Service");

    var builder = WebApplication.CreateBuilder(args);       // create app configuration obj [config, logging, DI]

    builder.Host.UseSerilog();

    // Standard Services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();     // swagger
    builder.Services.AddStandardApi();              // custom extension
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebClients", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    builder.Services.AddStandardAuth(builder.Configuration);

    // Service Specifics
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());      // mediator
    
    // register EF
    builder.Services.AddDbContext<IdentityDbContext>(options =>                                             
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb"));
    });
    builder.Services.AddScoped<IUserRepository, SqlUserRepository>();   //! IUserRepository -> SqlUserRepository [IUserRepository users -> system gives new SQlUserRepository()]

    // Messaging - SIMPLE with dedicated queue
    builder.Services.AddStandardMessaging(builder.Configuration, configure: null, serviceName: "identity");

    var app = builder.Build();

    // automatically apply db migration when app starts
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    // Standard Pipeline
    app.UseStandardApi("Identity Service API");         // custom middleware setup
    app.UseMiddleware<ExceptionHandlingMiddleware>();   // global error handling
    app.UseSerilogRequestLogging();                     // logs every request
    app.UseHttpsRedirection();
    app.UseCors("AllowWebClients");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();                                  // maps API routes

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Identity Service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
