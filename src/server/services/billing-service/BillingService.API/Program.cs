using BillingService.Infrastructure.Persistence.Sql;
using BillingService.API.Messaging;
using BillingService.Application.Abstractions.Persistence;
using BillingService.Application.Commands.Bills;
using BillingService.Application.Queries.Bills;
using BillingService.Infrastructure.Persistence.Sql.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License
Environment.SetEnvironmentVariable("MT_LICENSE", "free");

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);

// Service Specifics
builder.Services.AddMediatR(cfg => 
{
    cfg.RegisterServicesFromAssemblyContaining<MarkBillPaidCommandHandler>();
    cfg.RegisterServicesFromAssemblyContaining<GetMyBillsQueryHandler>();
});
builder.Services.AddDbContext<BillingDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb"));
});
builder.Services.AddScoped<IBillRepository, SqlBillRepository>();
builder.Services.AddScoped<IRewardRepository, SqlRewardRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddHttpClient();

// Messaging - SIMPLE with dedicated queue
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentReversedConsumer>();
}, "billing");

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await dbContext.Database.MigrateAsync();
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
