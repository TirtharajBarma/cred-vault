using Shared.Contracts.Middleware;
using Shared.Contracts.Extensions;
using CardService.Application.Abstractions.Persistence;
using CardService.Application.Commands.Cards;
using CardService.Application.Queries.Cards;
using CardService.API.Messaging;
using CardService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License
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

// Messaging - SIMPLE with dedicated queue
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
}, "card");

var app = builder.Build();

// 1. Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CardDbContext>();
    await dbContext.Database.MigrateAsync();
}

// 2. Routing FIRST (required for CORS to work properly)
app.UseRouting();

// 3. CORS - must be after UseRouting
app.UseCors("AllowWebClients");

// 4. Exception Handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 5. Rest of Pipeline
app.UseStandardApi("Card Service API");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
