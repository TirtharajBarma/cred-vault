using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using BillingService.Infrastructure.Persistence.Sql;

namespace BillingService.Infrastructure.Persistence.Sql;

public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BillingDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost,1434;Database=credvault_billing;User Id=sa;Password=Sql@Password!123;Encrypt=False;TrustServerCertificate=True;");
        return new BillingDbContext(optionsBuilder.Options);
    }
}
