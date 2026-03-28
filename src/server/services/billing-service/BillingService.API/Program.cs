using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Messaging;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Infrastructure.Persistence.Sql.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using BillingService.Domain.Entities;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License (Free tier for development)
Environment.SetEnvironmentVariable("MT_LICENSE", "free");

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);

// Service Specifics
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssemblyContaining<Program>();
    cfg.RegisterServicesFromAssemblyContaining<BillingService.Application.Commands.Bills.GenerateAdminBillCommand>();
    cfg.RegisterServicesFromAssemblyContaining<BillingService.Application.Commands.Bills.MarkBillPaidCommand>();
    cfg.RegisterServicesFromAssemblyContaining<BillingService.Application.Queries.Bills.GetMyBillsQuery>();
});

builder.Services.AddDbContext<BillingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb"));
});

builder.Services.AddScoped<IBillRepository, SqlBillRepository>();
builder.Services.AddScoped<IRewardRepository, SqlRewardRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// External Clients
builder.Services.AddHttpClient();

// Messaging
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
});

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await dbContext.Database.MigrateAsync();
    
        // Seed Reward Tiers if none exist
    if (!await dbContext.RewardTiers.AnyAsync())
    {
        var now = DateTime.UtcNow;
        var tiers = new List<BillingService.Domain.Entities.RewardTier>
        {
            new() { Id = Guid.NewGuid(), CardNetwork = Shared.Contracts.Enums.CardNetwork.Visa, IssuerId = null, MinSpend = 0, RewardRate = 0.02m, EffectiveFromUtc = now.AddDays(-30), EffectiveToUtc = null, CreatedAtUtc = now, UpdatedAtUtc = now },
            new() { Id = Guid.NewGuid(), CardNetwork = Shared.Contracts.Enums.CardNetwork.Visa, IssuerId = null, MinSpend = 1000, RewardRate = 0.03m, EffectiveFromUtc = now.AddDays(-30), EffectiveToUtc = null, CreatedAtUtc = now, UpdatedAtUtc = now },
            new() { Id = Guid.NewGuid(), CardNetwork = Shared.Contracts.Enums.CardNetwork.Visa, IssuerId = null, MinSpend = 5000, RewardRate = 0.05m, EffectiveFromUtc = now.AddDays(-30), EffectiveToUtc = null, CreatedAtUtc = now, UpdatedAtUtc = now },
            new() { Id = Guid.NewGuid(), CardNetwork = Shared.Contracts.Enums.CardNetwork.Mastercard, IssuerId = null, MinSpend = 0, RewardRate = 0.015m, EffectiveFromUtc = now.AddDays(-30), EffectiveToUtc = null, CreatedAtUtc = now, UpdatedAtUtc = now },
            new() { Id = Guid.NewGuid(), CardNetwork = Shared.Contracts.Enums.CardNetwork.Mastercard, IssuerId = null, MinSpend = 1000, RewardRate = 0.025m, EffectiveFromUtc = now.AddDays(-30), EffectiveToUtc = null, CreatedAtUtc = now, UpdatedAtUtc = now },
        };
        await dbContext.RewardTiers.AddRangeAsync(tiers);
        await dbContext.SaveChangesAsync();
    }
}

// Standard Pipeline
app.UseStandardApi("Billing Service API");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

