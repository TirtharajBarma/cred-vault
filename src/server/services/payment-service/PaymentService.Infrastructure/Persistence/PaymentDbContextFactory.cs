using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PaymentService.Infrastructure.Persistence.Sql;

namespace PaymentService.Infrastructure;

public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();

        var connectionString = Environment.GetEnvironmentVariable("PAYMENT_DB_CONNECTION") 
            ?? "Server=localhost,1434;Database=credvault_payments;User Id=sa;Password=Sql@Password!123;Encrypt=False;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);
        return new PaymentDbContext(optionsBuilder.Options);
    }
}
