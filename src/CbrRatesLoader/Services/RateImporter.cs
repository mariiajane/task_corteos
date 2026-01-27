using CbrRatesLoader.Data;
using CbrRatesLoader.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CbrRatesLoader.Services;

public sealed class RateImporter
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly CbrSoapClient _cbr;
    private readonly ILogger<RateImporter> _logger;

    public RateImporter(
        IDbContextFactory<AppDbContext> dbFactory,
        CbrSoapClient cbr,
        ILogger<RateImporter> logger)
    {
        _dbFactory = dbFactory;
        _cbr = cbr;
        _logger = logger;
    }

    public async Task ImportRangeAsync(DateOnly from, DateOnly to, bool skipIfDayAlreadyHasAnyRates, CancellationToken ct)
    {
        if (from > to) (from, to) = (to, from);

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            await ImportDayAsync(d, skipIfDayAlreadyHasAnyRates, ct);
        }
    }

    public async Task ImportDayAsync(DateOnly date, bool skipIfDayAlreadyHasAnyRates, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        if (skipIfDayAlreadyHasAnyRates)
        {
            var hasAny = await db.CurrencyRates.AnyAsync(r => r.Date == date, ct);
            if (hasAny)
            {
                _logger.LogInformation("Пропуск {Date}: данные уже есть.", date);
                return;
            }
        }

        IReadOnlyList<CbrRateDto> rates;
        try
        {
            rates = await _cbr.GetCursOnDateAsync(date, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения данных ЦБР за {Date}.", date);
            throw;
        }

        var useful = rates
            .Where(r => !string.IsNullOrWhiteSpace(r.CharCode))
            .Select(r => r with { CharCode = r.CharCode.Trim().ToUpperInvariant(), Name = r.Name.Trim() })
            .ToList();

        if (useful.Count == 0)
        {
            _logger.LogWarning("ЦБР вернул пустой список за {Date}.", date);
            return;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var codes = useful.Select(r => r.CharCode).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var existingCurrencies = await db.Currencies
            .Where(c => codes.Contains(c.CharCode))
            .ToListAsync(ct);

        var currencyByCode = existingCurrencies.ToDictionary(c => c.CharCode, StringComparer.OrdinalIgnoreCase);

        foreach (var r in useful)
        {
            if (!currencyByCode.TryGetValue(r.CharCode, out var currency))
            {
                currency = new Currency
                {
                    CbrCode = r.CbrCode,
                    CharCode = r.CharCode,
                    Name = r.Name
                };
                db.Currencies.Add(currency);
                currencyByCode[currency.CharCode] = currency;
            }
            else
            {
                // Обновляем справочник, если ЦБР поменял название/код.
                currency.CbrCode = r.CbrCode;
                currency.Name = string.IsNullOrWhiteSpace(r.Name) ? currency.Name : r.Name;
            }
        }

        await db.SaveChangesAsync(ct);

        var currencyIds = currencyByCode.Values.Select(c => c.Id).ToArray();

        var existingRates = await db.CurrencyRates
            .Where(cr => cr.Date == date && currencyIds.Contains(cr.CurrencyId))
            .ToListAsync(ct);

        var rateByCurrencyId = existingRates.ToDictionary(cr => cr.CurrencyId);

        var inserted = 0;
        var updated = 0;

        foreach (var r in useful)
        {
            var currency = currencyByCode[r.CharCode];
            if (!rateByCurrencyId.TryGetValue(currency.Id, out var rate))
            {
                db.CurrencyRates.Add(new CurrencyRate
                {
                    CurrencyId = currency.Id,
                    Date = date,
                    Nominal = r.Nominal,
                    Value = r.Value,
                    ImportedAtUtc = DateTime.UtcNow
                });
                inserted++;
            }
            else
            {
                if (rate.Nominal != r.Nominal || rate.Value != r.Value)
                {
                    rate.Nominal = r.Nominal;
                    rate.Value = r.Value;
                    rate.ImportedAtUtc = DateTime.UtcNow;
                    updated++;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Импорт {Date}: валют={Currencies}, вставлено={Inserted}, обновлено={Updated}.",
            date, useful.Count, inserted, updated);
    }
}

