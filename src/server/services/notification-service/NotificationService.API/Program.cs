using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Services;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Consumers;
using NotificationService.Application.Commands;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using Microsoft.EntityFrameworkCore;
using MediatR;
using MassTransit;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License (Free tier for development)
Environment.SetEnvironmentVariable("MT_LICENSE", "free");

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);

// Service Specifics
builder.Services.AddDbContext<NotificationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("NotificationDb"));
});

builder.Services.AddScoped<INotificationDbContext>(sp => sp.GetRequiredService<NotificationDbContext>());
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ProcessNotificationCommandHandler>());

// Messaging
builder.Services.AddMassTransit(x =>
{
    x.SetKebabCaseEndpointNameFormatter();
    x.AddConsumer<DomainEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"] ?? "rabbitmq", "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ReceiveEndpoint("notification-payment-completed", e =>
        {
            e.ConfigureConsumer<DomainEventConsumer>(context);
            e.UseMessageRetry(r => r.Intervals(1000, 2000, 5000));
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// Database Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<NotificationDbContext>();
        await context.Database.MigrateAsync();
        await SeedTemplatesAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Standard Pipeline
app.UseStandardApi("Notification Service API");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

async Task SeedTemplatesAsync(NotificationDbContext context)
{
    if (await context.EmailTemplates.AnyAsync()) return;

    var templates = new List<NotificationService.Domain.Entities.EmailTemplate>
    {
        new()
        {
            Id = Guid.NewGuid(),
            Name = "UserRegistered",
            SubjectTemplate = "Welcome to CredVault, {{FullName}}!",
            BodyTemplate = "Hi {{FullName}},\n\nWelcome to CredVault! Your account has been successfully created. Your User ID is {{UserId}}.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "UserOtpGenerated",
            SubjectTemplate = "Your CredVault OTP: {{OtpCode}}",
            BodyTemplate = "Hi {{FullName}},\n\nYour OTP for {{Reason}} is: {{OtpCode}}. It will expire at {{ExpiresAtUtc}}.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "CardAdded",
            SubjectTemplate = "New Card Added to CredVault",
            BodyTemplate = "Hi {{FullName}},\n\nA new card ending in {{CardNumberLast4}} has been added to your profile.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "BillGenerated",
            SubjectTemplate = "Your CredVault Bill is Ready",
            BodyTemplate = "Hi {{FullName}},\n\nA new bill for {{Amount}} is due on {{DueDate}}. Log in to your account to view details.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "PaymentOtpGenerated",
            SubjectTemplate = "Confirm your Payment of {{Amount}}",
            BodyTemplate = "Hi {{FullName}},\n\nUse this OTP to confirm your payment of {{Amount}}: {{OtpCode}}. Expires at {{ExpiresAtUtc}}.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "PaymentCompleted",
            SubjectTemplate = "Payment Confirmed - {{Amount}}",
            BodyTemplate = "Hi {{FullName}},\n\nYour payment of {{Amount}} has been confirmed.\n\nPayment ID: {{PaymentId}}\nAmount: {{Amount}}\n\nThank you for your payment!\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        },
        new()
        {
            Id = Guid.NewGuid(),
            Name = "PaymentFailed",
            SubjectTemplate = "Payment Failed - {{Amount}}",
            BodyTemplate = "Hi {{FullName}},\n\nUnfortunately, your payment of {{Amount}} could not be processed.\n\nPayment ID: {{PaymentId}}\nReason: {{Reason}}\n\nPlease try again or contact support.\n\nBest regards,\nCredVault Team",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }
    };

    context.EmailTemplates.AddRange(templates);
    await context.SaveChangesAsync();
}
