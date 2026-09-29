using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GamePort.Cashier.Infrastructure.Data;

public class CashierDbContextFactory : IDesignTimeDbContextFactory<CashierDbContext>
{
    public CashierDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CashierDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=WingPort;Username=postgres;Password=postgres");
        return new CashierDbContext(optionsBuilder.Options);
    }
}
