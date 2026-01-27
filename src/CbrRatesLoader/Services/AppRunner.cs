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

        // 1. Готовим базу (миграции)
        await _db.MigrateWithRetryAsync(ct);

        // 2. Первичная загрузка (за последние 30 дней)
        var tz = ResolveTimeZone(_cli.TimeZoneId);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz));
        var from = today.AddDays(-_cli.BackfillDays);

        _logger.LogInformation("Первичная загрузка за период: {From} .. {To}", from, today);
        // Используем skipIfDayAlreadyHasAnyRates: true, чтобы не перекачивать то, что уже есть
        await _importer.ImportRangeAsync(from, today, skipIfDayAlreadyHasAnyRates: true, ct);

        if (!_cli.Daemon)
        {
            _logger.LogInformation("Работа завершена (режим --once).");
            return;
        }

        // 3. Режим демона: спим до 15:05 каждого дня
        _logger.LogInformation("Режим фоновой службы активен. Ожидание обновлений в 15:05 ({Tz}).", tz.Id);

        while (!ct.IsCancellationRequested)
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);
            
            // Целевое время запуска: сегодня в 15:05
            var nextRun = now.Date.AddHours(15).AddMinutes(5);
            
            // Если 15:05 уже наступило или прошло, планируем на завтра
            if (now >= nextRun)
            {
                nextRun = nextRun.AddDays(1);
            }

            var delay = nextRun - now;
            _logger.LogInformation("Следующее обновление запланировано на {NextRun} (через {Delay}).", nextRun, delay);

            try 
            {
                await Task.Delay(delay, ct);
            }
            catch (OperationCanceledException) 
            {
                break; // Выход при остановке приложения
            }

            // После пробуждения скачиваем данные за текущую дату в зоне TZ
            var runDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz));
            _logger.LogInformation("Запуск ежедневного импорта за {Date}.", runDate);
            
            try
            {
                await _importer.ImportDayAsync(runDate, skipIfDayAlreadyHasAnyRates: false, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка во время ежедневного импорта. Попробуем снова через сутки.");
            }
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try 
        { 
            return TimeZoneInfo.FindSystemTimeZoneById(id); 
        }
        catch 
        { 
            // Кроссплатформенный fallback: если Linux не знает Windows-таймзону и наоборот
            return id == "Europe/Moscow" || id == "Russian Standard Time"
                ? TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow")
                : TimeZoneInfo.Utc;
        }
    }
}
