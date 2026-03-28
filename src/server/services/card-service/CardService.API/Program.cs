using Shared.Contracts.Middleware;
using Shared.Contracts.Extensions;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.API.Messaging;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<CreateCardCommand>());
builder.Services.AddDbContext<CardDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("CardDb"));
});
builder.Services.AddScoped<ICardRepository, SqlCardRepository>();

// Messaging
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
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

