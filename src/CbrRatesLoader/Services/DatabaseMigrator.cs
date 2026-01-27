using CbrRatesLoader.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CbrRatesLoader.Services;

public sealed class DatabaseMigrator
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(IDbContextFactory<AppDbContext> dbFactory, ILogger<DatabaseMigrator> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    public async Task MigrateWithRetryAsync(CancellationToken ct)
    {
        const int maxAttempts = 30;
        var delay = TimeSpan.FromSeconds(2);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                await db.Database.MigrateAsync(ct);

                _logger.LogInformation("База данных готова (Migrate).");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex, "БД ещё не готова (попытка {Attempt}/{Max}). Повтор через {Delay}s.",
                    attempt, maxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, ct);
            }
        }
    }
}

