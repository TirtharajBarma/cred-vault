using Shared.Contracts.Middleware;
using Shared.Contracts.Extensions;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Queries.Cards;
using CardService.API.Messaging;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using MassTransit;
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
    builder.Services.AddStandardApi();
    builder.Services.AddStandardCors();
    builder.Services.AddStandardAuth(builder.Configuration);
    builder.Services.AddHttpClient();

    builder.Services.AddMediatR(cfg => 
    {
        cfg.RegisterServicesFromAssemblyContaining<CreateCardCommand>();
        cfg.RegisterServicesFromAssemblyContaining<ListMyCardsQuery>();
    });
    builder.Services.AddDbContext<CardDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("CardDb")));
    builder.Services.AddScoped<ICardRepository, SqlCardRepository>();

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumer<PaymentCompletedConsumer>();
        x.AddConsumer<UserDeletedConsumer>();

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
                e.ConfigureConsumer<PaymentCompletedConsumer>(ctx);
                e.ConfigureConsumer<UserDeletedConsumer>(ctx);
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
