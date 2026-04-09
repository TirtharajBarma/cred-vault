using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using CardService.Infrastructure.Persistence.Sql;

namespace CardService.Infrastructure.Persistence.Sql;

public class CardDbContextFactory : IDesignTimeDbContextFactory<CardDbContext>
{
    public CardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CardDbContext>();
        
        var connectionString = Environment.GetEnvironmentVariable("CARD_DB_CONNECTION") 
            ?? "Server=localhost,1434;Database=credvault_cards;User Id=sa;Password=Sql@Password!123;Encrypt=False;TrustServerCertificate=True;";
        
        optionsBuilder.UseSqlServer(connectionString);
        return new CardDbContext(optionsBuilder.Options);
    }
}
