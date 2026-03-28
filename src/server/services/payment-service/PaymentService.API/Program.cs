using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Persistence.Sql;
using PaymentService.Application.Sagas;
using PaymentService.Domain.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Sql.Repositories;
using PaymentService.Infrastructure.Messaging.Consumers;
using PaymentService.Application.Common;
using PaymentService.Application.Commands.Payments;
using FluentValidation;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using MediatR;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License (Free tier for development)
Environment.SetEnvironmentVariable("MT_LICENSE", "free");

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);

// EF Core
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb")));

// External Clients
builder.Services.AddHttpClient("BillingServiceClient", client =>
{
    var baseUrl = builder.Configuration["Services:BillingService"] ?? "http://localhost:5003";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddHttpClient("IdentityServiceClient", client =>
{
    var baseUrl = builder.Configuration["Services:IdentityService:BaseUrl"] ?? "http://localhost:5001/";
    client.BaseAddress = new Uri(baseUrl);
});

// Repositories
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
builder.Services.AddScoped<IRiskRepository, RiskRepository>();
builder.Services.AddScoped<IFraudRepository, FraudRepository>();
builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());

// MediatR + FluentValidation pipeline
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssemblyContaining<InitiatePaymentCommand>();
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssemblyContaining<InitiatePaymentCommand>();

// Messaging + Saga
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<PaymentCompletedConsumer>();
    x.AddConsumer<PaymentFailedConsumer>();
    x.AddConsumer<FraudDetectedConsumer>();

    x.AddSagaStateMachine<PaymentSaga, PaymentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ExistingDbContext<PaymentDbContext>();
            r.UseSqlServer();
        });
});

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await dbContext.Database.MigrateAsync();
}

// Standard Pipeline
app.UseStandardApi("Payment Service API");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

