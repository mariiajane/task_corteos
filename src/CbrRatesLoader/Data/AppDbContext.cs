using CbrRatesLoader.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CbrRatesLoader.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Currency>(b =>
        {
            b.ToTable("currency");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.CbrCode).HasColumnName("cbr_code");
            b.Property(x => x.CharCode).HasColumnName("char_code").HasMaxLength(10).IsRequired();
            b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();

            b.HasIndex(x => x.CharCode).IsUnique();
        });

        modelBuilder.Entity<CurrencyRate>(b =>
        {
            b.ToTable("currency_rate");
            b.HasKey(x => x.Id);

            b.Property(x => x.Id).HasColumnName("id");
            b.Property(x => x.CurrencyId).HasColumnName("currency_id");
            b.Property(x => x.Date).HasColumnName("date");
            b.Property(x => x.Nominal).HasColumnName("nominal");
            b.Property(x => x.Value).HasColumnName("value").HasPrecision(18, 6);
            b.Property(x => x.ImportedAtUtc).HasColumnName("imported_at_utc");

            b.HasOne(x => x.Currency)
                .WithMany(x => x.Rates)
                .HasForeignKey(x => x.CurrencyId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(x => new { x.CurrencyId, x.Date }).IsUnique();
            b.HasIndex(x => x.Date);
        });
    }
}

