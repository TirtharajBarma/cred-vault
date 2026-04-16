using Microsoft.EntityFrameworkCore;
using PaymentService.Infrastructure.Persistence.Sql;
using PaymentService.Application.Sagas;
using PaymentService.Application.Sagas.Consumers;
using PaymentService.Domain.Interfaces;
using PaymentService.Domain.Entities;
using PaymentService.Infrastructure.Persistence.Sql.Repositories;
using PaymentService.Infrastructure.Messaging.Consumers;
using PaymentService.Infrastructure.Messaging;
using PaymentService.Infrastructure.BackgroundJobs;
using PaymentService.Application.Common;
using PaymentService.Application.Commands.Payments;
using PaymentService.Application.Services;
using FluentValidation;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using Shared.Contracts.Events.Identity;
using MediatR;
using MassTransit;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("MassTransit", LogEventLevel.Debug)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PaymentService")
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/payment-service-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
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
    builder.Services.AddStandardAuth(builder.Configuration);            // JWt token

    builder.Services.AddDbContext<PaymentDbContext>(o =>
    {
        o.UseSqlServer(builder.Configuration.GetConnectionString("PaymentDb"),
            x => x.MigrationsAssembly("PaymentService.Infrastructure"));
        o.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });

    builder.Services.AddHttpClient();
    builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
    builder.Services.AddScoped<ITransactionRepository, TransactionRepository>();
    builder.Services.AddScoped<IWalletRepository, WalletRepository>();
    builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<PaymentDbContext>());
    builder.Services.AddScoped<IWalletService, WalletService>();
    
    builder.Services.AddHostedService<PaymentExpirationBackgroundJob>();        // background job

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblyContaining<InitiatePaymentCommand>();               // auto register Commands, handlers
        cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));       // middleware for mediatR
    });
    builder.Services.AddValidatorsFromAssemblyContaining<InitiatePaymentCommand>();         // register FluentValidation

    builder.Services.AddMassTransit(x =>
    {
        x.SetKebabCaseEndpointNameFormatter();
        x.AddConsumer<PaymentCompletedConsumer>();
        x.AddConsumer<PaymentFailedConsumer>();
        x.AddConsumer<UserDeletedConsumer>();
        x.AddConsumer<UserRegisteredConsumer>();
        x.AddConsumer<PaymentProcessConsumer>();
        x.AddConsumer<RevertPaymentConsumer>();
        x.AddConsumer<RewardRedemptionConsumer>();
        x.AddConsumer<WalletRefundConsumer>();
        x.AddSagaStateMachine<PaymentOrchestrationSaga, PaymentOrchestrationSagaState>()
            .EntityFrameworkRepository(r => r.ExistingDbContext<PaymentDbContext>());

        x.UsingRabbitMq((ctx, cfg) =>
        {
            cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "localhost", "/", h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

            cfg.ReceiveEndpoint("payment-orchestration", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureSaga<PaymentOrchestrationSagaState>(ctx);
            });

            cfg.ReceiveEndpoint("payment-process", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureConsumer<PaymentProcessConsumer>(ctx);
                e.ConfigureConsumer<RevertPaymentConsumer>(ctx);
                e.ConfigureConsumer<RewardRedemptionConsumer>(ctx);
                e.ConfigureConsumer<WalletRefundConsumer>(ctx);
            });

            cfg.ReceiveEndpoint("payment-domain-event", e =>
            {
                e.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                e.UseInMemoryOutbox();
                e.ConfigureConsumer<PaymentCompletedConsumer>(ctx);
                e.ConfigureConsumer<PaymentFailedConsumer>(ctx);
                e.ConfigureConsumer<UserDeletedConsumer>(ctx);
                e.ConfigureConsumer<UserRegisteredConsumer>(ctx);
            });
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseStandardApi("Payment Service API");
    app.UseSerilogRequestLogging();
    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseHttpsRedirection();
    app.UseCors("AllowWebClients");
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    Log.Information("PaymentService started");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "PaymentService terminated");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
