using Microsoft.Extensions.Logging;

namespace CbrRatesLoader.Services;

public sealed class AppRunner
{
    private readonly CommandLineOptions _cli;
    private readonly DatabaseMigrator _db;
    private readonly RateImporter _importer;
    private readonly ILogger<AppRunner> _logger;

    public AppRunner(
        CommandLineOptions cli,
        DatabaseMigrator db,
        RateImporter importer,
        ILogger<AppRunner> logger)
    {
        _cli = cli;
        _db = db;
        _importer = importer;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("Старт. Режим: {Mode}.", _cli.Daemon ? "daemon" : "once");

        await _db.MigrateWithRetryAsync(ct);

        var tz = ResolveTimeZone(_cli.TimeZoneId);
        var todayLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date);

        var from = _cli.From ?? todayLocal.AddDays(-(Math.Max(1, _cli.BackfillDays) - 1));
        var to = _cli.To ?? todayLocal;

        if (from > to)
        {
            (from, to) = (to, from);
        }

        _logger.LogInformation("Загрузка диапазона: {From} .. {To} (TZ: {Tz}).", from, to, tz.Id);
        await _importer.ImportRangeAsync(from, to, skipIfDayAlreadyHasAnyRates: false, ct);

        if (!_cli.Daemon)
        {
            _logger.LogInformation("Готово. Завершение.");
            return;
        }

        _logger.LogInformation("Переход в режим ежедневного запуска в {RunAt} ({Tz}).",
            _cli.RunAtLocalTime, tz.Id);

        while (!ct.IsCancellationRequested)
        {
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

            var nextRunDateTimeLocal = nowLocal.Date + _cli.RunAtLocalTime.ToTimeSpan();
            var nextRunLocal = new DateTimeOffset(nextRunDateTimeLocal, tz.GetUtcOffset(nextRunDateTimeLocal));
            if (nowLocal >= nextRunLocal)
            {
                nextRunDateTimeLocal = nextRunDateTimeLocal.AddDays(1);
                nextRunLocal = new DateTimeOffset(nextRunDateTimeLocal, tz.GetUtcOffset(nextRunDateTimeLocal));
            }

            var delay = nextRunLocal - nowLocal;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            _logger.LogInformation("Следующий запуск: {NextRunLocal} (через {Delay}).", nextRunLocal, delay);
            await Task.Delay(delay, ct);

            var runDateLocal = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date);
            _logger.LogInformation("Ежедневная выгрузка за {Date}.", runDateLocal);
            await _importer.ImportDayAsync(runDateLocal, skipIfDayAlreadyHasAnyRates: false, ct);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        // На Linux чаще "Europe/Moscow", на Windows — "Russian Standard Time".
        if (TryFind(id, out var tz)) return tz;

        var fallbacks = id switch
        {
            "Europe/Moscow" => new[] { "Russian Standard Time" },
            "Russian Standard Time" => new[] { "Europe/Moscow" },
            _ => new[] { "Europe/Moscow", "Russian Standard Time", "UTC" }
        };

        foreach (var fb in fallbacks)
        {
            if (TryFind(fb, out tz)) return tz;
        }

        return TimeZoneInfo.Utc;
    }

    private static bool TryFind(string id, out TimeZoneInfo tz)
    {
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch
        {
            tz = null!;
            return false;
        }
    }
}

