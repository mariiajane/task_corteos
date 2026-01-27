namespace CbrRatesLoader.Data.Entities;

public sealed class CurrencyRate
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CurrencyId { get; set; }
    public Currency Currency { get; set; } = null!;

    /// <summary>
    /// Дата, на которую установлен курс (по Москве).
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Номинал (например, 1, 10, 100).
    /// </summary>
    public int Nominal { get; set; }

    /// <summary>
    /// Значение курса (рублей за Nominal единиц валюты).
    /// </summary>
    public decimal Value { get; set; }

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;
}

