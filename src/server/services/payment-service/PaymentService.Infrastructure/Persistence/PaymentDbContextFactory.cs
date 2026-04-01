using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PaymentService.Infrastructure.Persistence.Sql;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentDbContext>();

        optionsBuilder.UseSqlServer("Server=localhost,1434;Database=credvault_payments;User Id=sa;Password=Sql@Password!123;Encrypt=False;TrustServerCertificate=True");

        return new PaymentDbContext(optionsBuilder.Options);
    }
}
