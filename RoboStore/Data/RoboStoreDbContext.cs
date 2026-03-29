using Microsoft.EntityFrameworkCore;
using RoboStore.Models;

namespace RoboStore.Data;

public class RoboStoreDbContext : DbContext
{
    public DbSet<User> Users { get; set; }

    private readonly string _connectionString;

    public RoboStoreDbContext()
    {
        _connectionString = @"Server=RoboStore.mssql.somee.com;Database=RoboStore;User Id=MomentoMori_SQLLogin_1;Password=8rhd2k6i2g;TrustServerCertificate=True";
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer(_connectionString);
    }
}
