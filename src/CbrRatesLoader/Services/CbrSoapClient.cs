using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace CbrRatesLoader.Services;

public sealed class CbrSoapClient
{
    private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

    private readonly HttpClient _http;
    private readonly AppConfig _config;
    private readonly ILogger<CbrSoapClient> _logger;

    public CbrSoapClient(HttpClient http, AppConfig config, ILogger<CbrSoapClient> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CbrRateDto>> GetCursOnDateAsync(DateOnly date, CancellationToken ct)
    {
        var dateTime = date.ToDateTime(TimeOnly.MinValue);
        var envelope = BuildSoap11Envelope(dateTime);

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.CbrEndpoint)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.Add("SOAPAction", "\"http://web.cbr.ru/GetCursOnDateXML\"");

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(ct);
        return ParseGetCursOnDateXmlResponse(body);
    }

    private static string BuildSoap11Envelope(DateTime onDate)
    {
        // Формат соответствует xsd:dateTime. Сервис ЦБР принимает локальную дату/время без строгой зоны.
        var dt = onDate.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

        return $"""
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
               xmlns:xsd="http://www.w3.org/2001/XMLSchema"
               xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
  <soap:Body>
    <GetCursOnDateXML xmlns="http://web.cbr.ru/">
      <On_date>{dt}</On_date>
    </GetCursOnDateXML>
  </soap:Body>
</soap:Envelope>
""";
    }

    private IReadOnlyList<CbrRateDto> ParseGetCursOnDateXmlResponse(string soapXml)
    {
        XDocument soapDoc;
        try
        {
            soapDoc = XDocument.Parse(soapXml);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось распарсить SOAP-ответ как XML.");
            throw;
        }

        var result = soapDoc
            .Descendants()
            .FirstOrDefault(x => x.Name.LocalName == "GetCursOnDateXMLResult");

        if (result is null)
        {
            throw new InvalidOperationException("В SOAP-ответе не найден элемент GetCursOnDateXMLResult.");
        }

        XElement? valuteDataRoot = null;

        // Вариант 1: результат вложен как XML-элемент(ы)
        if (result.Elements().Any())
        {
            valuteDataRoot = result.Elements().First();
        }
        else
        {
            // Вариант 2: результат — строка (возможно с XML внутри)
            var inner = result.Value.Trim();
            if (!inner.StartsWith('<'))
            {
                throw new InvalidOperationException("GetCursOnDateXMLResult не содержит XML.");
            }

            var innerDoc = XDocument.Parse(inner);
            valuteDataRoot = innerDoc.Root;
        }

        if (valuteDataRoot is null)
        {
            throw new InvalidOperationException("Не удалось извлечь ValuteData из SOAP-ответа.");
        }

        // Структура: <ValuteData><ValuteCursOnDate>...</ValuteCursOnDate>...</ValuteData>
        var items = valuteDataRoot
            .Descendants()
            .Where(x => x.Name.LocalName == "ValuteCursOnDate")
            .Select(ParseValuteCursOnDate)
            .ToList();

        if (items.Count == 0)
        {
            _logger.LogWarning("На дату {Date} сервис вернул 0 валют.", valuteDataRoot.Attribute("OnDate")?.Value);
        }

        return items;
    }

    private static CbrRateDto ParseValuteCursOnDate(XElement x)
    {
        static string GetString(XElement e, string name) =>
            e.Elements().FirstOrDefault(z => z.Name.LocalName == name)?.Value?.Trim() ?? string.Empty;

        static int GetInt(XElement e, string name)
        {
            var s = GetString(e, name);
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : 0;
        }

        static decimal GetDecimalRu(XElement e, string name)
        {
            var s = GetString(e, name);
            return decimal.TryParse(s, NumberStyles.Number, RuCulture, out var d) ? d : 0m;
        }

        var cbrCode = GetInt(x, "Vcode");
        var charCode = GetString(x, "VchCode");
        var name = GetString(x, "Vname");
        var nominal = GetInt(x, "Vnom");
        var value = GetDecimalRu(x, "Vcurs");

        return new CbrRateDto(
            CbrCode: cbrCode,
            CharCode: charCode,
            Name: name,
            Nominal: nominal,
            Value: value);
    }
}

public sealed record CbrRateDto(int CbrCode, string CharCode, string Name, int Nominal, decimal Value);

