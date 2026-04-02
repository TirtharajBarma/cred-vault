using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Messaging;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Commands.Statements;
using BillingService.Application.Queries.Bills;
using BillingService.Application.Queries.Statements;
using BillingService.Infrastructure.Persistence.Sql.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using MassTransit;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "BillingService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/billing-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Environment.SetEnvironmentVariable("MT_LICENSE", "free");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddStandardApi();
    builder.Services.AddStandardCors();
    builder.Services.AddStandardAuth(builder.Configuration);

    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssemblyContaining<MarkBillPaidCommandHandler>();
        cfg.RegisterServicesFromAssemblyContaining<GetMyBillsQueryHandler>();
        cfg.RegisterServicesFromAssemblyContaining<GenerateStatementCommandHandler>();
        cfg.RegisterServicesFromAssemblyContaining<GetMyStatementsQueryHandler>();
    });
    builder.Services.AddDbContext<BillingDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb")));
    builder.Services.AddScoped<IBillRepository, SqlBillRepository>();
    builder.Services.AddScoped<IRewardRepository, SqlRewardRepository>();
    builder.Services.AddScoped<IStatementRepository, SqlStatementRepository>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddHttpClient();

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumer<UserDeletedConsumer>();
        x.AddConsumer<BillUpdateSagaConsumer>();
        x.AddConsumer<RevertBillSagaConsumer>();

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("billing-domain-event", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureConsumer<UserDeletedConsumer>(ctx);
                e.ConfigureConsumer<BillUpdateSagaConsumer>(ctx);
                e.ConfigureConsumer<RevertBillSagaConsumer>(ctx);
            });
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseStandardApi("Billing Service API");
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();
    app.UseCors("AllowWebClients");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("BillingService started");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BillingService terminated");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
