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
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "CardService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/card-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
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
    builder.Services.AddHttpClient();

    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssemblyContaining<CreateCardCommand>();
        cfg.RegisterServicesFromAssemblyContaining<ListMyCardsQuery>();
    });
    builder.Services.AddDbContext<CardDbContext>(o =>
    {
        o.UseSqlServer(builder.Configuration.GetConnectionString("CardDb"));
        o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
    builder.Services.AddScoped<ICardRepository, SqlCardRepository>();
    builder.Services.AddHttpClient<IBillingServiceClient, BillingServiceClient>();

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumer<PaymentReversedConsumer>();
        x.AddConsumer<UserDeletedConsumer>();
        x.AddConsumer<CardDeductionSagaConsumer>();

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("card-domain-event", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureConsumeTopology = false;

                e.Bind<IUserDeleted>();
                e.Bind<ICardDeductionRequested>();
                e.Bind<IPaymentReversed>();

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
