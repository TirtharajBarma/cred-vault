using Shared.Contracts.Middleware;
using Shared.Contracts.Extensions;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Queries.Cards;
using CardService.API.Messaging;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License (Free tier for development)
Environment.SetEnvironmentVariable("MT_LICENSE", "free");

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);
builder.Services.AddHttpClient();

// Service Specifics
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssemblyContaining<CreateCardCommand>();
    cfg.RegisterServicesFromAssemblyContaining<ListMyCardsQuery>();
});
builder.Services.AddDbContext<CardDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CardDb"));
});
builder.Services.AddScoped<ICardRepository, SqlCardRepository>();

// Messaging
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<PaymentCompletedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("card-payment-completed", e =>
        {
            e.ConfigureConsumer<PaymentCompletedConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(1000, 2000, 5000));
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CardDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Standard Pipeline
app.UseStandardApi("Card Service API");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

