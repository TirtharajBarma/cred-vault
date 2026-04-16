using Shared.Contracts.Middleware;
using Shared.Contracts.Extensions;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Interfaces;
using CardService.Application.Commands.Cards;
using CardService.Application.Queries.Cards;
using CardService.API.Messaging;
using CardService.Infrastructure.Persistence.Sql;
using CardService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Shared.Contracts.Events.Identity;
using Shared.Contracts.Events.Payment;
using Shared.Contracts.Events.Saga;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .Enrich.FromLogContext()                                                             // Adds extra info to logs
    .Enrich.WithProperty("Application", "CardService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/card-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Environment.SetEnvironmentVariable("MT_LICENSE", "free");       // requires license settings for massTransit

    var builder = WebApplication.CreateBuilder(args);               // DI, config system, logging system
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();                     // swagger
    builder.Services.AddStandardApi();                              // swagger
    
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowWebClients", policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
    
    builder.Services.AddStandardAuth(builder.Configuration);        // setup JWT/identity
    builder.Services.AddHttpClient();                               // register HttpClient globally [DI]
    builder.Services.AddDataProtection();                           // used for encryption / token protection -> enables -> IDataProtectionProvider

    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssemblyContaining<CreateCardCommand>();
        cfg.RegisterServicesFromAssemblyContaining<ListMyCardsQuery>();
    });
    
    builder.Services.AddDbContext<CardDbContext>(o =>                           // register DB connection using EF
    {
        o.UseSqlServer(builder.Configuration.GetConnectionString("CardDb"));
        o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
    
    builder.Services.AddScoped<ICardRepository, SqlCardRepository>();                   // DI mapping with interface and implementation
    builder.Services.AddHttpClient<IBillingServiceClient, BillingServiceClient>();      //! special httpClient for Billing Service

    // x -> massTransit setUp 
    builder.Services.AddMassTransit(x =>                                    // register MassTransit in DI
    {
        x.SetKebabCaseEndpointNameFormatter();                              // naming convention
        x.AddConsumer<PaymentReversedConsumer>();                           // handles payment reversal    [events]
        x.AddConsumer<UserDeletedConsumer>();                               // handle user deletion        [events]  
        x.AddConsumer<CardDeductionSagaConsumer>();                         // handle saga workflow        [events]

        x.UsingRabbitMq((ctx, cfg) =>                                       // rabbitmq server connect
        // cfg -> rabbitmq config
        // ctx -> DI container access
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("card-domain-event", e =>                  // endpoint ->  queue name [card-domain-event]
            // e -> queue config
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();                                     // prevent duplicate message processing
                e.ConfigureConsumeTopology = false;                        // manually bind events

                // listen to these event-types from rabbitmq
                e.Bind<IUserDeleted>();
                e.Bind<ICardDeductionRequested>();
                e.Bind<IPaymentReversed>();

                // connect event -> consumer [when events comes who will handle it (consumer)]
                //! publish -> rabbitmq -> queue -> consumer -> logic
                e.ConfigureConsumer<PaymentReversedConsumer>(ctx);
                e.ConfigureConsumer<UserDeletedConsumer>(ctx);
                e.ConfigureConsumer<CardDeductionSagaConsumer>(ctx);
            });
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<CardDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseRouting();
    app.UseCors("AllowWebClients");
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseStandardApi("Card Service API");
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("CardService started");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "CardService terminated");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
