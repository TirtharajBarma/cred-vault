using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Messaging;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Queries.Bills;
using BillingService.Application.Queries.Statements;
using BillingService.Infrastructure.Persistence.Sql.Repositories;
using BillingService.API.Services;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using Shared.Contracts.Events.Identity;
using Shared.Contracts.Events.Saga;
using Shared.Contracts.Events.Payment;
using MassTransit;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .Enrich.FromLogContext()                                                        // add extra metadata to logs
    .Enrich.WithProperty("Application", "BillingService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/billing-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Environment.SetEnvironmentVariable("MT_LICENSE", "free");

    var builder = WebApplication.CreateBuilder(args);               // DI + config
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddStandardApi();                              // custom extension
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebClients", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    builder.Services.AddStandardAuth(builder.Configuration);            // custom auth setup

    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssemblyContaining<MarkBillPaidCommandHandler>();
        cfg.RegisterServicesFromAssemblyContaining<GetMyBillsQueryHandler>();
        cfg.RegisterServicesFromAssemblyContaining<GetMyStatementsQueryHandler>();
    });
    builder.Services.AddDbContext<BillingDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb")));
    builder.Services.AddScoped<IBillRepository, SqlBillRepository>();               // DI
    builder.Services.AddScoped<IRewardRepository, SqlRewardRepository>();
    builder.Services.AddScoped<IStatementRepository, SqlStatementRepository>();
    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddHttpClient();                                               // external API's
    builder.Services.AddHostedService<OverdueBillScheduler>();

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();

        // register this class in DI so that it can process messages
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
                e.ConfigureConsumeTopology = false;

                // listen to these event-types from rabbitmq
                e.Bind<IUserDeleted>();
                e.Bind<IBillUpdateRequested>();
                e.Bind<IRevertBillUpdateRequested>();

                // connect event -> consumer [when events comes who will handle it (consumer)]
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
