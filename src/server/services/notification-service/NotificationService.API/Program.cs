using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Services;
using NotificationService.Application.Interfaces;
using NotificationService.Application.Consumers;
using NotificationService.Application.Commands;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using Microsoft.EntityFrameworkCore;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// MassTransit License
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
builder.Services.AddScoped<IEmailSender, GmailSmtpEmailSender>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<ProcessNotificationCommandHandler>());

// Messaging - SIMPLE with dedicated queue
builder.Services.AddStandardMessaging(builder.Configuration, x =>
{
    x.AddConsumer<DomainEventConsumer>();
}, "notification");

var app = builder.Build();

// Database Migration
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<NotificationDbContext>();
    await context.Database.MigrateAsync();
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
