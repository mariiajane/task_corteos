namespace CbrRatesLoader.Data.Entities;

public sealed class Currency
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Числовой код валюты в системе ЦБР (поле Vcode).
    /// </summary>
    public int CbrCode { get; set; }

    /// <summary>
    /// Буквенный код (USD, EUR и т.п.).
    /// </summary>
    public string CharCode { get; set; } = string.Empty;

    /// <summary>
    /// Наименование валюты.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public List<CurrencyRate> Rates { get; set; } = new();
}

