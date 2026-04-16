using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Services;              // gives -> GmailSmtpEmailSender
using NotificationService.Application.Interfaces;
using NotificationService.Application.Consumers;
using NotificationService.Application.Commands;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Serilog;
using Serilog.Events;
using MassTransit;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithProperty("Application", "NotificationService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/notification-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Environment.SetEnvironmentVariable("MT_LICENSE", "free");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddStandardApi();
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

    // connects sql server
    builder.Services.AddDbContext<NotificationDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb")));
    builder.Services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());           // interface -> DI
    builder.Services.AddScoped<IEmailSender, GmailSmtpEmailSender>();

    // Registers all handlers in that assembly
    builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ProcessNotificationCommandHandler>());

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumer<DomainEventConsumer>();

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("notification-domain-event", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureConsumer<DomainEventConsumer>(ctx);      // link consumer to queue
            });
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseStandardApi("Notification Service API");
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();
    app.UseCors("AllowWebClients");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("NotificationService started on port 5005");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Service terminated");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
