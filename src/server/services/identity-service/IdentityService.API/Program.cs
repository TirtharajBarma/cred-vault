using IdentityService.Infrastructure.Persistence.Sql;
using Microsoft.EntityFrameworkCore;
using Shared.Contracts.Extensions;
using Shared.Contracts.Middleware;
using IdentityService.Application.Abstractions.Persistence;
using IdentityService.Application.Commands.Auth;
using IdentityService.Application.Common;

var builder = WebApplication.CreateBuilder(args);

// Standard Services
builder.Services.AddControllers();
builder.Services.AddStandardApi();
builder.Services.AddStandardCors();
builder.Services.AddStandardAuth(builder.Configuration);

// Service Specifics
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<RegisterCommand>());
builder.Services.AddDbContext<IdentityDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("IdentityDb"));
});
builder.Services.AddScoped<IUserRepository, SqlUserRepository>();

// Messaging
builder.Services.AddStandardMessaging(builder.Configuration);

var app = builder.Build();

// Database Migration & Seeding
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await dbContext.Database.MigrateAsync();
    await IdentityDataSeeder.SeedAsync(dbContext);
}

// Standard Pipeline
app.UseStandardApi("Identity Service API");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowWebClients");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

